using ApiServer.DB;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Providers;

public class CachedDataProvider
{
    private record DailyProductSnapshot(int ProductId, int Probability);
    private record FreeProductSnapshot(int ProductId);

    private readonly List<DailyProductSnapshot> _dailyProductSnapshots;
    private readonly List<FreeProductSnapshot> _freeProductSnapshots;
    private readonly Random _random = new();
    
    public CachedDataProvider(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        using var context = dbContextFactory.CreateDbContext();
        
        _dailyProductSnapshots = context.DailyProduct.AsNoTracking()
            .Select(dp => new DailyProductSnapshot(dp.ProductId, dp.Weight))
            .ToList();
        
        _freeProductSnapshots = context.Product.AsNoTracking()
            .Where(p => p.Price == 0)
            .Select(p => new FreeProductSnapshot(p.ProductId))
            .ToList();
    }

    public List<int> GetRandomDailyProductsDistinct(int count)
    {
        if (count <= 0) return new List<int>();
        if (count >= _dailyProductSnapshots.Count) return _dailyProductSnapshots.Select(dps => dps.ProductId).ToList();
        
        var pool = _dailyProductSnapshots
            .Select(dps => new DailyProductSnapshot(dps.ProductId, dps.Probability)).ToList();
        var result = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            var totalWeight = pool.Sum(p => p.Probability);
            var random = _random.Next(1, totalWeight + 1);
            var accumulatedWeight = 0;
            for (var j = 0; j < pool.Count; j++)
            {
                accumulatedWeight += pool[j].Probability;
                if (random <= accumulatedWeight)
                {
                    result.Add(pool[j].ProductId);
                    pool.RemoveAt(j);
                    break;
                }
            }
        }

        return result;
    }

    public int GetRandomFreeProduct()
    {
        var randomIndex = _random.Next(0, _freeProductSnapshots.Count);
        return _freeProductSnapshots[randomIndex].ProductId;
    }

    // public List<int> GetRandomDailyProductsForClosedPicks(int cout)
    // {
    //     
    // }
}