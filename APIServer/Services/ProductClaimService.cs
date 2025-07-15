using ApiServer.DB;
using ApiServer.Providers;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public class ProductClaimService
{
    private readonly AppDbContext _context;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<RewardService> _logger;    
    
    private readonly Random _random = new();
    
    public ProductClaimService(AppDbContext context, CachedDataProvider cachedDataProvider, ILogger<RewardService> logger)
    {
        _context = context;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }

    /// <summary>
    /// Unpack products in mailbox to user product table.
    /// </summary>
    public void UnpackPackages(int userId, List<Mail> mails)
    {
        var productIds = mails
            .Where(m =>
            {
                if (m.ProductId != null) return true;
                _logger.LogWarning("MailId {MailId} has null ProductId, skipping unpacking.", m.MailId);
                return false;
            })
            .Select(m => m.ProductId!.Value)
            .ToArray();
        var productDictionary = ClassifyProductsById(productIds);
        var userProducts = _context.UserProduct
            .Where(up => up.UserId == userId && up.AcquisitionPath == AcquisitionPath.Open)
            .ToList();

        foreach (var compositions in productDictionary.Values)
        {
            foreach (var composition in compositions)
            {
                if (userProducts.Select(up => up.ProductId).Contains(composition.ProductId))
                {
                    var existingProduct = userProducts.First(up => up.ProductId == composition.ProductId);
                    existingProduct.Count += compositions.Count;
                }
                else
                {
                    _context.UserProduct.Add(new UserProduct
                    {
                        UserId = userId,
                        ProductId = composition.ProductId,
                        Count = composition.Count,
                        ProductType = composition.ProductType,
                        AcquisitionPath = AcquisitionPath.Open
                    });
                }
            }
        }
        
        _context.SaveChangesExtended();
    }

    public async Task<ClaimData> ClassifyAndClaim(int userId, List<int> productIds, ClaimData? data = null)
    {
        data ??= new ClaimData();
        
        var productDict = ClassifyProducts(userId, productIds.ToArray());
        
        if (productDict.TryGetValue(ProductOpenType.Select, out var selectableProducts))
        {
            if (selectableProducts.Count > 0)
            {
                data = await ClaimSelectableProduct(userId, selectableProducts);
            }
        }
        else if (productDict.TryGetValue(ProductOpenType.Random, out var randomProducts))
        {
            if (randomProducts.Count > 0)
            {
                // Go to next random popup
                data = await ClaimRandomProduct(userId, randomProducts);
            }
        }
        else if (productDict.TryGetValue(ProductOpenType.Single, out var singleProducts))
        {
            if (singleProducts.Count > 0)
            {
                // Go to next single popup
                data = await ClaimSingleProduct(userId, singleProducts);
            }
        }
        else
        {
            // No products to claim
            data.RewardPopupType = RewardPopupType.None;
        }

        return data;
    }

    public async Task<ClaimData> ClaimSelectableProduct(int userId, List<ProductComposition> compositions)
    {
        var data = new ClaimData();
        var userProductList = await _context.UserProduct
            .Where(up => up.UserId == userId && up.AcquisitionPath == AcquisitionPath.Open)
            .ToListAsync();
        var productId = compositions.First().ProductId;
        var userProductSelectable = userProductList.FirstOrDefault(up => up.ProductId == productId);
        var pcList = _cachedDataProvider.GetProductCompositions().Where(pc => pc.ProductId == productId).ToList();
        
        // Go to next select popup
        if (userProductSelectable != null)
        {
            data.ProductInfos.Add(MapProductInfo(userProductSelectable));
            data.CompositionInfos.AddRange(pcList.Select(MapCompositionInfo));
            data.RewardPopupType = RewardPopupType.Select;
        }
        
        return data;
    }
    
    public async Task<ClaimData> ClaimRandomProduct(int userId, List<ProductComposition> compositions)
    {
        var data = new ClaimData();
        foreach (var composition in compositions)
        {
            var userProduct = _context.UserProduct
                .FirstOrDefault(up => up.ProductId == composition.ProductId);
            if (userProduct != null)
            {
                data.RandomProductInfos.Add(new RandomProductInfo
                {
                    ProductInfo = MapProductInfo(userProduct),
                    Count = composition.Count
                });
                
                var product = _cachedDataProvider.GetProducts()
                    .FirstOrDefault(p => p.ProductId == userProduct.ProductId);
                if (product != null)
                {
                    var drawList = DrawRandomProduct(product, userProduct.Count);
                    foreach (var pc in drawList)
                    {
                        var compositionInfo = MapCompositionInfo(pc);
                        var existingComposition = data.CompositionInfos
                            .FirstOrDefault(ci => ci.CompositionId == pc.CompositionId &&
                                                  ci.ProductType == pc.ProductType);

                        if (existingComposition != null)
                        {
                            existingComposition.Count += pc.Count;
                        }
                        else
                        {
                            data.CompositionInfos.Add(compositionInfo);
                        }
                        
                        StoreProduct(userId, pc);
                        AddDisplayingComposition(userId, compositionInfo);
                    }
                }
                
                RemoveUserProduct(userId, composition.ProductId, composition.Count);
            }
        }
        
        data.RewardPopupType = RewardPopupType.Open;
        return data;
    }

    public async Task<ClaimData> ClaimSingleProduct(int userId, List<ProductComposition> compositions)
    {
        var data = new ClaimData();
        foreach (var composition in compositions)
        {
            var userProduct = _context.UserProduct
                .FirstOrDefault(up => up.ProductId == composition.ProductId);
            if (userProduct != null)
            {
                var compositionInfo = MapCompositionInfo(composition);
                var existingComposition = data.CompositionInfos
                    .FirstOrDefault(ci => ci.CompositionId == composition.CompositionId &&
                                          ci.ProductType == composition.ProductType);
                            
                if (existingComposition != null)
                {
                    existingComposition.Count += composition.Count;
                }
                else
                {
                    data.CompositionInfos.Add(compositionInfo);
                }
                            
                data.ProductInfos.Add(MapProductInfo(userProduct));
                StoreProduct(userId, composition);
                AddDisplayingComposition(userId, compositionInfo);
                RemoveUserProduct(userId, composition.ProductId);
            }
        }
        
        if (_cachedDataProvider.DisplayingCompositions.TryRemove(userId, out var dispCompositions))
        {
            data.CompositionInfos = dispCompositions.Items.ToList();
        }
        
        data.RewardPopupType = RewardPopupType.Item;
        return data;
    }
    
    public Dictionary<ProductOpenType, List<ProductComposition>> ClassifyProductsById(int[] productIds)
    {
        var result = new Dictionary<ProductOpenType, List<ProductComposition>>();
        
        // ex) productIds : { 6, 54 }
        foreach (var productId in productIds)
        {
            var dictionary = ClassifyProduct(productId);
            result = MergeProductDictionary(result, dictionary);
        }

        return result;
    }    
    
    public Dictionary<ProductOpenType, List<ProductComposition>> ClassifyProducts(int userId, int[] productIds)
    {
        var result = new Dictionary<ProductOpenType, List<ProductComposition>>();
        
        var userProducts = _context.UserProduct
            .Where(up => up.UserId == userId)
            .ToList();

        // ex) productIds : { 6, 54 }
        foreach (var userProduct in userProducts)
        {
            var dictionary = ClassifyProduct(userProduct.ProductId, userProduct.ProductType, userProduct.Count);
            result = MergeProductDictionary(result, dictionary);
        }

        return result;
    }
    
    private Dictionary<ProductOpenType, List<ProductComposition>> ClassifyProduct(
        int productId, ProductType productType = ProductType.None, int count = 1)
    {
        var result = new Dictionary<ProductOpenType, List<ProductComposition>>();
        var productList = _cachedDataProvider.GetProducts();
        // ex) productId : { 53 }, compositionId : { 37, ..., 54 }
        var compositionList = _cachedDataProvider.GetProductCompositions()
            .Select(pc => new ProductComposition
            {
                ProductId = pc.ProductId,
                CompositionId = pc.CompositionId,
                ProductType = pc.ProductType,
                Count = pc.Count,
                Guaranteed = pc.Guaranteed,
                IsSelectable = pc.IsSelectable
            })
            .Where(pc => pc.ProductId == productId)
            .ToList();
        var probList = _cachedDataProvider.GetProbabilities();

        if (compositionList.Count == 0 || compositionList.All(pc => pc.ProductType != ProductType.None))
        {
            var composition = new ProductComposition
            {
                ProductId = productId,
                Count = count,
                ProductType = productType
            };
            
            if (compositionList.All(pc => pc.Guaranteed))
            {
                result = AddProductToDictionary(result, ProductOpenType.Single, composition);
            }
            else
            {
                if (compositionList.All(pc => pc.IsSelectable))
                {
                    result = AddProductToDictionary(result, ProductOpenType.Select, composition);
                }
                else if (compositionList.All(pc => pc.IsSelectable == false))
                {
                    result = AddProductToDictionary(result, ProductOpenType.Random, composition);
                }
            }
        }
        else
        {
            var compositionPairs = compositionList
                .Select(pc => (pc.ProductId, pc.CompositionId))
                .ToHashSet();
            var probPairs = probList
                .Where(cp => cp.ProductId == productId)
                .Select(cp => (cp.ProductId, cp.CompositionId))
                .ToHashSet();
            // composition쌍과 prob 쌍이 완전 일치 -> Product count가 랜덤인 상황 ex) product id = 52, 54 경우
            var missing = compositionPairs.Except(probPairs);
            
            if (missing.Any() == false)
            {
                var compositionInfo = Draw(productId);
                var rProduct = productList.First(p => p.ProductId == compositionInfo.CompositionId);
                var dictionary = ClassifyProduct(rProduct.ProductId, rProduct.ProductType, compositionInfo.Count);
                return MergeProductDictionary(result, dictionary);
            }

            foreach (var composition in compositionList)
            {
                if (composition.Count == 0)
                {
                    var compositionInfo = Draw(composition.ProductId);
                    composition.Count = compositionInfo.Count;
                }
                
                var dictionary = ClassifyProduct(composition.CompositionId, composition.ProductType, composition.Count);
                result = MergeProductDictionary(result, dictionary);
            }
        }

        return result;
    }
    
    public List<ProductComposition> GetFinalProducts(int productId)
    {
        /* Example of GetFinalProducts output for productId
         한마디로 1번만 깐다
        Input: [1, 32, 33]
        ┌─ ProductId──┐
        │   Select : 32, 33, 38, 39, 32, 33
        │   Random : 49, 50
        │   Single : 127, 527, 4001
        └──────────────────────────
        Input: [21]
        ┌─ ProductId──┐
        │   Random : 57, 58, 59
        └──────────────────────────
        */
        var compositions = _cachedDataProvider.GetProductCompositions()
            .Where(pc => pc.ProductId == productId)
            .ToList();
        var result = new List<ProductComposition>();

        foreach (var composition in compositions)
        {
            if (composition.ProductType == ProductType.None)
            {
                var subResults = GetFinalProducts(composition.CompositionId);
                result.AddRange(subResults);
            }
            else
            {
                if (compositions.Count > 1 && composition.Guaranteed == false)
                {
                    if (compositions.All(pc => pc.IsSelectable))
                    {
                        result.Add(composition);
                    }
                    else if (compositions.All(pc => pc.IsSelectable == false))
                    {
                        result.Add(composition);
                    }
                }
                else
                {
                    result.Add(composition);
                }
            }
        }

        return result;
    }
    
    public List<ProductComposition> DrawRandomProduct(Product product, int count = 1)
    {
        var result = new List<ProductComposition>();
        var productList = _cachedDataProvider.GetProducts();
        var compositionList = _cachedDataProvider.GetProductCompositions()
            .Where(pc => pc.ProductId == product.ProductId)
            .ToList();
        var probList = _cachedDataProvider.GetProbabilities();

        if (compositionList.All(pc => pc.ProductType != ProductType.None))
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(Draw(product.ProductId));
            }
            
            return result;
        }

        if (compositionList.All(pc => pc.ProductType == ProductType.None))
        {
            var compositionPairs = compositionList
                .Select(p => (p.ProductId, p.CompositionId))
                .ToHashSet();
            var probPairs = probList
                .Where(p => p.ProductId == product.ProductId)
                .Select(p => (p.ProductId, p.CompositionId))
                .ToHashSet();
            var missing = compositionPairs.Except(probPairs);
            
            // composition쌍과 prob 쌍이 완전 일치 -> Product count가 랜덤인 상황 ex) product id = 54, 52 경우
            if (missing.Any() == false)
            {
                var compositionInfo = Draw(product.ProductId);
                var rProduct = productList.First(p => p.ProductId == compositionInfo.CompositionId);
                result.AddRange(DrawRandomProduct(rProduct, compositionInfo.Count));
                return result;
            }

            foreach (var pc in compositionList)
            {
                var drawCount = pc.Count;
                var rProduct = productList.First(p => p.ProductId == pc.CompositionId);
                if (drawCount == 0)
                {
                    drawCount = Draw(pc.ProductId).Count;
                }
                    
                result.AddRange(DrawRandomProduct(rProduct, drawCount));
            }
        }

        return result;
    }
    
    private ProductComposition Draw(int productId)
    {
        var probList = _cachedDataProvider.GetProbabilities().Where(cp => cp.ProductId == productId).ToList();
        var compositionList = _cachedDataProvider.GetProductCompositions();
        double randValue = _random.NextDouble();
        double cumulative = 0.0;
        foreach (var compositionProb in probList)
        {
            cumulative += compositionProb.Probability;
            if (randValue <= cumulative)
            {
                var compositionId = compositionProb.CompositionId;
                var compositionInfo = compositionList
                    .FirstOrDefault(pc => pc.ProductId == productId && pc.CompositionId == compositionId);
                if (compositionInfo != null)
                {
                    var compositionCopied = new ProductComposition
                    {
                        ProductId = compositionInfo.ProductId,
                        CompositionId = compositionInfo.CompositionId,
                        ProductType = compositionInfo.ProductType,
                        Count = compositionProb.Count, 
                        Guaranteed = compositionInfo.Guaranteed,
                        IsSelectable = compositionInfo.IsSelectable
                    };

                    return compositionCopied;
                }
            }
        }
        
        var lastCompositionInfo = compositionList
            .FirstOrDefault(pc => pc.ProductId == productId && pc.CompositionId == probList.Last().CompositionId);
        return lastCompositionInfo ?? new ProductComposition();
    }

    public void RemoveUserProduct(int userId, int productId, int count = 1)
    {
        var existing = _context.UserProduct
            .FirstOrDefault(up => up.ProductId == productId && up.UserId == userId);

        if (existing != null)
        {
            if (existing.Count - count > 0)
            {
                existing.Count -= count;
            }
            else
            {
                _context.UserProduct.Remove(existing);
            }
        }
        
        _context.SaveChangesExtended();
    }
    
    public void StoreProduct(int userId, ProductComposition pc)
    {
        switch (pc.ProductType)
        {
            case ProductType.Unit:
                var existingUserUnit = _context.UserUnit
                    .FirstOrDefault(uu => uu.UserId == userId && uu.UnitId == (UnitId)pc.CompositionId);
                if (existingUserUnit == null)
                {
                    _context.UserUnit.Add(new UserUnit
                    {
                        UserId = userId,
                        UnitId = (UnitId)pc.CompositionId,
                        Count = pc.Count
                    });
                }
                else
                {
                    existingUserUnit.Count += pc.Count;
                }
                break;
            
            case ProductType.Material:
                var existingUserMaterial = _context.UserMaterial
                    .FirstOrDefault(um => um.UserId == userId && um.MaterialId == (MaterialId)pc.CompositionId);
                if (existingUserMaterial == null)
                {
                    _context.UserMaterial.Add(new UserMaterial
                    {
                        UserId = userId,
                        MaterialId = (MaterialId)pc.CompositionId,
                        Count = pc.Count
                    });
                }
                else
                {
                    existingUserMaterial.Count += pc.Count;
                }
                break;
            
            case ProductType.Enchant:
                var existingUserEnchant = _context.UserEnchant
                    .FirstOrDefault(ue => ue.UserId == userId && ue.EnchantId == (EnchantId)pc.CompositionId);
                if (existingUserEnchant == null)
                {
                    _context.UserEnchant.Add(new UserEnchant
                    {
                        UserId = userId,
                        EnchantId = (EnchantId)pc.CompositionId,
                        Count = pc.Count
                    });
                }
                else
                {
                    existingUserEnchant.Count += pc.Count;
                }
                break;
            
            case ProductType.Sheep:
                var existingUserSheep = _context.UserSheep
                    .FirstOrDefault(us => us.UserId == userId && us.SheepId == (SheepId)pc.CompositionId);
                if (existingUserSheep == null)
                {
                    _context.UserSheep.Add(new UserSheep
                    {
                        UserId = userId,
                        SheepId = (SheepId)pc.CompositionId,
                        Count = pc.Count
                    });
                }
                else
                {
                    existingUserSheep.Count += pc.Count;
                }
                break;
            
            case ProductType.Character:
                var existingUserCharacter = _context.UserCharacter
                    .FirstOrDefault(uc => uc.UserId == userId && uc.CharacterId == (CharacterId)pc.CompositionId);
                if (existingUserCharacter == null)
                {
                    _context.UserCharacter.Add(new UserCharacter
                    {
                        UserId = userId,
                        CharacterId = (CharacterId)pc.CompositionId,
                        Count = pc.Count
                    });
                }
                else
                {
                    existingUserCharacter.Count += pc.Count;
                }
                break;
            
            case ProductType.Gold:
                var userStatGold = _context.UserStats
                    .FirstOrDefault(us => us.UserId == userId);
                if (userStatGold != null)
                {
                    userStatGold.Gold += pc.Count;
                }
                break;
            
            case ProductType.Spinel:
                var userStatSpinel = _context.UserStats
                    .FirstOrDefault(us => us.UserId == userId);
                if (userStatSpinel != null)
                {
                    userStatSpinel.Spinel += pc.Count;
                }
                break;

            case ProductType.None:
                
                break;
        }
        
        _context.SaveChangesExtended();
    }
    
    private Dictionary<ProductOpenType, List<ProductComposition>> MergeProductDictionary(
        Dictionary<ProductOpenType, List<ProductComposition>> dict1,
        Dictionary<ProductOpenType, List<ProductComposition>> dict2)
    {
        return dict1.Concat(dict2)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.SelectMany(kvp => kvp.Value).ToList());
    }

    private Dictionary<ProductOpenType, List<ProductComposition>> AddProductToDictionary(
        Dictionary<ProductOpenType, List<ProductComposition>> dict,
        ProductOpenType openType,
        ProductComposition composition)
    {
        if (!dict.ContainsKey(openType))
        {
            dict[openType] = new List<ProductComposition>();
        }
        dict[openType].Add(composition);
        return dict;
    }
    
    public ProductInfo MapProductInfo(Product product)
    {
        return new ProductInfo
        {
            ProductId = product.ProductId,
            Compositions = _cachedDataProvider.GetProductCompositions()
                .Where(pc => pc.ProductId == product.ProductId)
                .Select(MapCompositionInfo)
                .ToList(),
            ProductType = product.ProductType,
            Price = product.Price,
            Category = product.Category,
            CurrencyType = product.Currency,
            ProductCode = product.ProductCode,
        };
    }
    
    public ProductInfo MapProductInfo(UserProduct userProduct)
    {
        var product = _cachedDataProvider.GetProducts().FirstOrDefault(p => p.ProductId == userProduct.ProductId);
        return new ProductInfo
        {
            ProductId = userProduct.ProductId,
            Compositions = _cachedDataProvider.GetProductCompositions()
                .Where(pc => pc.ProductId == userProduct.ProductId)
                .Select(MapCompositionInfo)
                .ToList(),
            ProductType = userProduct.ProductType,
            Price = product?.Price ?? 0,
            Category = product?.Category ?? ProductCategory.None,
            CurrencyType = product?.Currency ?? CurrencyType.None,
            ProductCode = product?.ProductCode ?? string.Empty,
        };
    }
    
    public CompositionInfo MapCompositionInfo(ProductComposition pc) => new()
    {
        ProductId = pc.ProductId,
        CompositionId = pc.CompositionId,
        ProductType = pc.ProductType,
        Count = pc.Count,
        MinCount = _cachedDataProvider.GetProbabilityLookups()
            .GetValueOrDefault((pc.ProductId, pc.CompositionId), (0, 0)).Item1,
        MaxCount = _cachedDataProvider.GetProbabilityLookups()
            .GetValueOrDefault((pc.ProductId, pc.CompositionId), (0, 0)).Item2,
        Guaranteed = pc.Guaranteed,
        IsSelectable = pc.IsSelectable
    };
    
    public void AddDisplayingComposition(int userId, CompositionInfo compositionInfo)
    {
        var dispCompositions = _cachedDataProvider.DisplayingCompositions.GetOrAdd(userId, 
            _ => new DisplayingCompositions());
        dispCompositions.AddOrIncrement(compositionInfo);
    }
}