using ApiServer.DB;
using ApiServer.Providers;
using NuGet.Packaging;

namespace ApiServer.Services;

public class RewardService
{
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<RewardService> _logger;
    private readonly List<StageInfo> _stageInfos;
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
    
    public RewardService(CachedDataProvider cachedDataProvider, ILogger<RewardService> logger)
    {
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
        _stageInfos = _cachedDataProvider.GetStageInfos();
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

        rewards.AddRange(_stageInfos
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
        var candidates = Enum.GetValues(typeof(MaterialId)).Cast<int>()
            .Where(v => v != 2000 && v != 2036).ToList();
        
        if (count > candidates.Count) count = candidates.Count;

        for (int i = 0; i < count; i++)
        {
            int j = _random.Next(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        return candidates.Take(count).Select(id => new RewardInfo
        {
            ItemId = id,
            ProductType = ProductType.Material,
            Count = 1
        }).ToList();
    }
}