using System.Diagnostics.CodeAnalysis;
using ApiServer.DB;
using ApiServer.Util;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Providers;

public class CachedDataProvider
{
    public record DailyProductSnapshot(int ProductId, int Weight, int Price, UnitClass Class);
    private record FreeProductSnapshot(int ProductId, int Weight, UnitClass Class);

    private readonly Dictionary<int, int> _expSnapshots;
    private readonly List<DailyProductSnapshot> _dailyProductSnapshots;
    private readonly List<FreeProductSnapshot> _freeProductSnapshots;
    private readonly Random _random = new();
    
    public int QueueCountsSheep { get; set; } = 0;
    public int QueueCountsWolf { get; set; } = 0;
    
    public Dictionary<int, int> GetExpSnapshots() => _expSnapshots;
    public List<DailyProductSnapshot> GetDailyProductSnapshots() => _dailyProductSnapshots;

    public CachedDataProvider(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        using var context = dbContextFactory.CreateDbContext();
        
        _expSnapshots = context.Exp.AsNoTracking()
            .ToDictionary(exp => exp.Level, exp => exp.Exp);
        
        _dailyProductSnapshots = context.DailyProduct.AsNoTracking()
            .Select(dp => new DailyProductSnapshot(dp.ProductId, dp.Weight, dp.Price, dp.Class))
            .ToList();
        
        _freeProductSnapshots = context.DailyFreeProduct.AsNoTracking()
            .Select(p => new FreeProductSnapshot(p.ProductId, p.Weight, p.Class))
            .ToList();
    }

    public List<int> GetRandomDailyProductsDistinct(int count)
    {
        var random = new Random();
        var pool = _dailyProductSnapshots
            .Select(dp => new WeightedItem<DailyProductSnapshot>(dp, dp.Weight))
            .ToList();
        
        var result = new List<int>(count);

        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var pickedItem = pool.PopRandomByWeight(random);
            result.Add(pickedItem.Item.ProductId);
        }
        
        return result;
    }

    public int GetRandomFreeProduct()
    {
        var pool = _freeProductSnapshots
            .Select(fp => new WeightedItem<FreeProductSnapshot>(fp, fp.Weight))
            .ToList();
        return pool.PopRandomByWeight(_random).Item.ProductId;
    }

    public List<int> GetRandomDailyProductsForClosedPicks(int count)
    {
        var random = new Random();
        var result = new List<int>(count);

        for (var i = 0; i < count; i++)
        {
            var randomValue = random.Next(0, 100);
            var filteredClass = randomValue switch
            {
                >= 40 and < 100 => UnitClass.Knight,
                >= 10 and < 40 => UnitClass.NobleKnight,
                _ => UnitClass.Baron,
            };

            var filteredItems = _dailyProductSnapshots
                .Where(dp => dp.Class == filteredClass)
                .Select(dp => new WeightedItem<int>(dp.ProductId, dp.Weight))
                .ToList();
            
            if (filteredItems.Count == 0) break; // No more items to pick
            var pickedItem = filteredItems.PopRandomByWeight(random);
            result.Add(pickedItem.Item);
        }

        return result;
    }

    public int GetDailyProductPrice(int productId)
    {
        return _dailyProductSnapshots.FirstOrDefault(dp => dp.ProductId == productId)?.Price ?? int.MaxValue;
    }
}