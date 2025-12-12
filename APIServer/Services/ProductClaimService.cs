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
    public async Task UnpackPackages(int userId, List<Mail> mails)
    {
        // 0) 유효 productId만 추출
        var productIds = mails
            .Where(m =>
            {
                if (m.ProductId != null) return true;
                _logger.LogWarning("MailId {MailId} has null ProductId, skipping unpacking.", m.MailId);
                return false;
            })
            .Select(m => m.ProductId!.Value)
            .ToArray();

        if (productIds.Length == 0)
        {
            _logger.LogWarning("No valid ProductIds found in mails for userId {UserId}, marking mails as claimed.", userId);
            foreach (var mail in mails) mail.Claimed = true;
            await _context.SaveChangesExtendedAsync();
            return;
        }
        
        var cachedCompList = _cachedDataProvider.GetProductCompositions()
            .Where(pc => productIds.Contains(pc.ProductId))
            .ToList();
        
        foreach (var productId in productIds)
        {
            var compositions = cachedCompList
                .Where(pc => pc.ProductId == productId)
                .ToList();
            if (compositions.All(pc => pc is { Guaranteed: true, IsSelectable: false }))
            {
                // container or single
                Unpack(compositions);
            }
            else
            {
                // selectable or random
                StageUserProduct(productId, 1);
            }
        }
        
        
        
        
        // 1) 구성 분류 후 전부 집계 (Single/Random/Select 최종 상품 옮김)
        Dictionary<ProductOpenType, List<ProductComposition>> productDictionary = ClassifyProductsById(productIds);

        // ProductId -> (누적수량, 타입)
        var deltaByProductId = new Dictionary<int, (int Count, ProductType Type)>();
        foreach (var composition in productDictionary.Values.SelectMany(list => list))
        {
            if (!deltaByProductId.TryGetValue(composition.ProductId, out var cur))
            {
                deltaByProductId[composition.ProductId] = (composition.Count, composition.ProductType);
            }        
            else
            {
                deltaByProductId[composition.ProductId] = (cur.Count + composition.Count, cur.Type);
            }    
        }

        if (deltaByProductId.Count == 0)
        {
            foreach (var mail in mails) mail.Claimed = true;
            await _context.SaveChangesExtendedAsync();
            return;
        }

        var pidList = deltaByProductId.Keys.ToList();

        // 2) 기존 보유분 로드 (같은 PK 중복 Add 방지)
        var existing = await _context.UserProduct
            .Where(up => up.UserId == userId && pidList.Contains(up.ProductId))
            .ToListAsync();

        var map = existing.ToDictionary(up => up.ProductId);

        // 3) 업서트
        foreach (var (pid, info) in deltaByProductId)
        {
            if (map.TryGetValue(pid, out var up))
            {
                up.Count += info.Count;
                up.ProductType = info.Type;             // 필요 시 동기화
                up.AcquisitionPath = AcquisitionPath.Open;
            }
            else
            {
                var newUp = new UserProduct
                {
                    UserId = userId,
                    ProductId = pid,
                    Count = info.Count,
                    ProductType = info.Type,
                    AcquisitionPath = AcquisitionPath.Open
                };
                _context.UserProduct.Add(newUp);
                map[pid] = newUp; // 다음 루프에서 중복 Add 방지
            }
        }

        // 4) 메일 클레임
        foreach (var mail in mails) mail.Claimed = true;

        await _context.SaveChangesExtendedAsync();
    }

    // ex) Overpower(1) 같은 single 상품 1 -> 32, 33 .... 4001
    private void Unpack(List<ProductComposition> compositions)
    {
        var table = _context.UserProduct;
        foreach (var composition in compositions)          
        {
            if (composition.ProductType != 0)
            {
                
            }
            else
            {
                
            }
        }
    }
    
    // ex) Top Grade Material Box(21) 같은 이번 iteration에 개봉해야 하는 상품
    private void StageUserProduct(int compositionId, int count)
    {
        
    }
    
    // 선택 -> 랜덤 -> 싱글 -> 구독 순으로 팝업 우선순위 결정
    public async Task<ClaimData> ClassifyAndClaim(int userId)
    {
        var data = new ClaimData();
        
        var productDict = ClassifyProducts(userId);
        
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
    
    // 하나 받고 userProductSelectable이 남아있으면 다음 선택 팝업으로 이동
    private async Task<ClaimData> ClaimSelectableProduct(int userId, List<ProductComposition> compositions)
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
    
    private async Task<ClaimData> ClaimRandomProduct(int userId, List<ProductComposition> compositions)
    {
        var data = new ClaimData();
        foreach (var composition in compositions)
        {
            var userProduct = await _context.UserProduct
                .FirstOrDefaultAsync(up => up.ProductId == composition.ProductId);
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
                        
                        await StoreProductAsync(userId, pc);
                        AddDisplayingComposition(userId, compositionInfo);
                    }
                }
                
                RemoveUserProduct(userId, composition.ProductId, composition.Count);
            }
        }
        
        data.RewardPopupType = RewardPopupType.Open;
        return data;
    }

    private async Task<ClaimData> ClaimSingleProduct(int userId, List<ProductComposition> compositions)
    {
        var data = new ClaimData();
        foreach (var composition in compositions)
        {
            var userProduct = await _context.UserProduct
                .FirstOrDefaultAsync(up => up.ProductId == composition.ProductId);
            if (userProduct != null) await ClaimProduct(userId, composition, data);
        }
        
        data.RewardPopupType = RewardPopupType.Item;
        return data;
    }
    
    private async Task ClaimProduct(int userId, ProductComposition composition, ClaimData data)
    {
        var pc = new ProductComposition
        {
            CompositionId = composition.ProductId,
            ProductType = composition.ProductType,
            Count = composition.Count
        };
        var compositionInfo = MapCompositionInfo(pc);
        var existingComposition = data.CompositionInfos
            .FirstOrDefault(ci => ci.CompositionId == composition.CompositionId &&
                                  ci.ProductType == composition.ProductType);
                            
        if (existingComposition != null)
        {
            existingComposition.Count += pc.Count;
        }
        else
        {
            data.CompositionInfos.Add(compositionInfo);
        }
                            
        await StoreProductAsync(userId, pc);
        AddDisplayingComposition(userId, compositionInfo);
        RemoveUserProduct(userId, composition.ProductId, composition.Count);
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
    
    public Dictionary<ProductOpenType, List<ProductComposition>> ClassifyProducts(int userId)
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
        int productId, ProductType productType = ProductType.Container, int count = 1)
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

        // unit, spinel 등 productType가 None이 아닌 경우
        if (compositionList.Count == 0 || compositionList.All(pc => pc.ProductType != ProductType.Container))
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
    
    public List<ProductComposition> DrawRandomProduct(Product product, int count = 1)
    {
        var result = new List<ProductComposition>();
        var productList = _cachedDataProvider.GetProducts();
        var compositionList = _cachedDataProvider.GetProductCompositions()
            .Where(pc => pc.ProductId == product.ProductId)
            .ToList();
        var probList = _cachedDataProvider.GetProbabilities();

        if (compositionList.All(pc => pc.ProductType != ProductType.Container))
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(Draw(product.ProductId));
            }
            
            return result;
        }

        if (compositionList.All(pc => pc.ProductType == ProductType.Container))
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
            if (existing.Count - count > 0) existing.Count -= count;
            else  _context.UserProduct.Remove(existing);
        }
        
        _context.SaveChangesExtended();
    }
    
    public async Task StoreProductAsync(int userId, ProductComposition pc)
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
            
            case ProductType.Exp:
                var userStatExp = _context.UserStats
                    .FirstOrDefault(us => us.UserId == userId);
                if (userStatExp != null)
                {
                    var level = userStatExp.UserLevel;
                    userStatExp.Exp += pc.Count;
                    if (userStatExp.Exp >= _cachedDataProvider.GetExpSnapshots()[level])
                    {
                        userStatExp.UserLevel++;
                        userStatExp.Exp -= _cachedDataProvider.GetExpSnapshots()[level];
                    }
                }
                break;
            
            case ProductType.Subscription:
                var nowUtc = DateTime.UtcNow;
                var lifetimeUtc = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                var subType = SubscriptionType.AdsRemover; 
                
                // 1) 아직 남아있는(만료 안 됐고, 취소도 안 된) 같은 타입 구독이 있으면 무시
                var existingActiveSub = _context.UserSubscription
                    .FirstOrDefault(us =>
                        us.UserId == userId &&
                        us.SubscriptionType == subType &&
                        us.IsCanceled == false &&
                        us.ExpiresAtUtc > nowUtc);

                if (existingActiveSub != null)
                    break; // 그냥 무시하고 종료
                
                _context.UserSubscription.Add(new UserSubscription
                {
                    UserId = userId,
                    SubscriptionType = subType,
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = lifetimeUtc,
                    IsCanceled = false,
                    IsTrial = false,
                });

                _context.UserSubscriptionHistory.Add(new UserSubscriptionHistory
                {
                    UserId = userId,
                    SubscriptionType = subType,
                    FromUtc = nowUtc,
                    ToUtc = lifetimeUtc,
                    EventType = SubscriptionEvent.Started
                });
                break;
            
            case ProductType.Container:
                
                break;
        }
        
        await _context.SaveChangesExtendedAsync();
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