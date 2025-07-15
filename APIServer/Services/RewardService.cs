using ApiServer.DB;
using ApiServer.Providers;
using NuGet.Packaging;

namespace ApiServer.Services;

public class RewardService
{
    private readonly AppDbContext _context;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<RewardService> _logger;
    
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
    
    public RewardService(AppDbContext context, CachedDataProvider cachedDataProvider, ILogger<RewardService> logger)
    {
        _context = context;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
        
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

        // Gold Reward
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
        
        // Other Rewards
        rewards.AddRange(win ? GetRandomMaterial(3) : GetRandomMaterial(1));
        rewards.Add(win 
            ? new RewardInfo { ItemId = 1, ProductType = ProductType.Exp, Count = 50 }
            : new RewardInfo { ItemId = 1, ProductType = ProductType.Exp, Count = 10 });

        return rewards;
    }

    public List<SingleRewardInfo> GetSingleRewards(int stageId, int star)
    {
        if (star == 0 ) return new List<SingleRewardInfo>();

        var rewards = new List<SingleRewardInfo>();

        rewards.AddRange(StageInfos
            .FirstOrDefault(si => si.StageId == stageId)?.StageReward
            .Where(sr => sr.Star <= star)
            .Select(sr => new SingleRewardInfo
            {
                ItemId = sr.ProductId,
                ProductType = sr.ProductType,
                Count = sr.Count,
                Star = sr.Star
            }).ToList() ?? throw new InvalidOperationException());
        
        rewards.Add(new SingleRewardInfo { ItemId = 1, ProductType = ProductType.Exp, Count = 30 });
        
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
}