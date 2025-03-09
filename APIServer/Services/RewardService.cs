using ApiServer.DB;

namespace ApiServer.Services;

public class RewardService
{
    private readonly AppDbContext _context;
    
    private readonly Random _random = new();
    
    private readonly Dictionary<(int Min, int Max), List<RewardInfo>> _rankRewardInfos = new()
    {
        { (0, 499), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 80 } } },
        { (500, 999), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 100 } } },
        { (1000, 1199), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 125 } } },
        { (1200, 1399), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 160 } } },
        { (1400, 1599), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 200 } } },
        { (1600, 1799), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 250 } } },
        { (1800, 1899), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 320 } } },
        { (1900, 1999), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 400 } } },
        { (2000, 10000), new List<RewardInfo> { new() { ItemId = 1, ProductType = ProductType.Gold, Count = 500 } } },
    };
    
    private readonly Dictionary<(int Min, int Max), List<RewardInfo>> _levelRewardInfos = new()
    {
        
    };
    
    public List<StageInfo> StageInfos { get; set; } = new();
    
    public RewardService(AppDbContext context)
    {
        _context = context;
        
        List<StageEnemy> stageEnemies = _context.StageEnemy.ToList();
        List<StageReward> stageRewards = _context.StageReward.ToList();
        
        foreach (var stage in _context.Stage)
        {
            var stageInfo = new StageInfo
            {
                StageId = stage.StageId,
                StageLevel = stage.StageLevel,
                UserFaction = stage.UserFaction,
                AssetId = stage.AssetId,
                CharacterId = stage.CharacterId,
                MapId = stage.MapId,
                StageEnemy = stageEnemies.FindAll(se => se.StageId == stage.StageId),
                StageReward = stageRewards.FindAll(sr => sr.StageId == stage.StageId)
            };
            StageInfos.Add(stageInfo);
        }
    }
    
    public List<RewardInfo> GetRankRewards(int userId, int rankPoint, int rankPointValue, bool win)
    {
        var rewards = new List<RewardInfo>();

        foreach (var (range, rewardInfo) in _rankRewardInfos)
        {
            if (rankPoint + rankPointValue < range.Min || rankPoint + rankPointValue > range.Max) continue;
            var goldReward = rewardInfo.FirstOrDefault(r => r.ProductType == ProductType.Gold);
            var gold = goldReward?.Count ?? 0;
            rewards.Add(new RewardInfo
            {
                ItemId = 1,
                ProductType = ProductType.Gold,
                Count = win ? gold + rankPointValue : gold - rankPointValue
            });
            break;
        }

        rewards.AddRange(win ? GetRandomMaterial(3) : GetRandomMaterial(1));

        return rewards;
    }

    public List<SingleRewardInfo> GetSingleRewards(int stageId, int star)
    {
        if (star == 0 ) return new List<SingleRewardInfo>();
        
        var rewards = StageInfos
            .FirstOrDefault(si => si.StageId == stageId)?.StageReward
            .Where(sr => sr.Star <= star)
            .Select(sr => new SingleRewardInfo
            {
                ItemId = sr.ProductId,
                ProductType = sr.ProductType,
                Count = sr.Count,
                Star = sr.Star
            }).ToList();
        
        if (rewards == null) return new List<SingleRewardInfo>();
        
        foreach (var reward in rewards)
        {
            Console.WriteLine(reward);
        }

        return rewards;
    }
    
    private List<RewardInfo> GetRandomMaterial(int count)
    {
        // Excepting 0 and rainbow egg
        var values = Enum.GetValues(typeof(MaterialId)).Cast<int>().ToArray();
        var min = values.Min();
        var max = values.Max();
        var valuesCount = Enumerable.Range(min + 1, max - 1).ToList();
        var random = new Random();
        var selectedValues = valuesCount.OrderBy(_ => random.Next()).Take(count).ToList();
        
        return selectedValues
            .Select(materialId => new RewardInfo { ItemId = materialId, ProductType = ProductType.Material, Count = 1 })
            .ToList();
    }

    public List<ProductComposition> ClaimFinalProducts(int productId)
    {
        var visited = new HashSet<int>();
        return ClaimFinalProductsInternal(productId, visited);
    }

    private List<ProductComposition> ClaimFinalProductsInternal(int productId, HashSet<int> visited)
    {
        if (!visited.Add(productId))
        {
            return new List<ProductComposition>();
        }

        var compositions = _context.ProductComposition
            .Where(pc => pc.ProductId == productId)
            .ToList();
        var result = new List<ProductComposition>();

        foreach (var composition in compositions)
        {
            if (composition.Guaranteed)
            {
                if (composition.Type == ProductType.None)
                {
                    var subResults = ClaimFinalProductsInternal(composition.CompositionId, visited);
                    result.AddRange(subResults);
                }
                else
                {
                    result.Add(composition);
                }
            }
            else
            {
                var probList = _context.CompositionProbability
                    .Where(cp => cp.ProductId == composition.ProductId)
                    .ToList();

                if (probList.Count == 0) continue;
                
                var chosenProductId = SelectedRandomProduct(probList);
                var subResults = ClaimFinalProductsInternal(chosenProductId, visited);
                result.AddRange(subResults);
            }
        }

        return result;
    }

    private int SelectedRandomProduct(List<CompositionProbability> probList)
    {
        double totalProb = probList.Sum(cp => cp.Probability);
        double randValue = _random.NextDouble() * totalProb;
        double cumulative = 0.0;

        foreach (var probability in probList)
        {
            cumulative += probability.Probability;
            if (randValue <= cumulative)
            {
                return probability.CompositionId;
            }
        }

        return probList.Last().CompositionId;
    }
    
    public void ClaimPurchasedProduct(int userId, ProductComposition pc)
    {
        switch (pc.Type)
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
        }
    }
}