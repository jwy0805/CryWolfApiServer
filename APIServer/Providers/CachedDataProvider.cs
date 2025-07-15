using System.Collections.Concurrent;
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
    private readonly List<MaterialInfo> _materialInfos = new();
    private readonly List<Product> _products;
    private readonly List<ProductComposition> _productCompositions;
    private readonly List<CompositionProbability> _probabilities;
    private readonly Dictionary<(int ProductId, int CompositionId), (int min, int max)> _probabilityLookups;
    private readonly List<DailyProductSnapshot> _dailyProductSnapshots;
    private readonly List<FreeProductSnapshot> _freeProductSnapshots;
    private readonly Random _random = new();
    
    public int QueueCountsSheep { get; set; } = 0;
    public int QueueCountsWolf { get; set; } = 0;
    
    public Dictionary<int, int> GetExpSnapshots() => _expSnapshots;
    public List<MaterialInfo> GetMaterialInfos() => _materialInfos;
    public List<Product> GetProducts() => _products;
    public List<ProductComposition> GetProductCompositions() => _productCompositions;
    public List<CompositionProbability> GetProbabilities() => _probabilities;
    public Dictionary<(int ProductId, int CompositionId), (int min, int max)> GetProbabilityLookups() => _probabilityLookups;
    public List<DailyProductSnapshot> GetDailyProductSnapshots() => _dailyProductSnapshots;
    public ConcurrentDictionary<int, DisplayingCompositions> DisplayingCompositions { get; set; } = new();
    
    public CachedDataProvider(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        using var context = dbContextFactory.CreateDbContext();
        
        _expSnapshots = context.Exp.AsNoTracking()
            .ToDictionary(exp => exp.Level, exp => exp.Exp);

        _materialInfos = context.Material.AsNoTracking().Select(m => new MaterialInfo
        {
            Id = (int)m.MaterialId,
            Class = m.Class,
        }).ToList();
        _products = context.Product.AsNoTracking().ToList();
        _productCompositions = context.ProductComposition.AsNoTracking().ToList();
        _probabilities = context.CompositionProbability.AsNoTracking().ToList();

        _probabilityLookups = _probabilities
            .GroupBy(cp => (cp.ProductId, cp.CompositionId))
            .ToDictionary(
                grouping => grouping.Key, 
                grouping => (Min: grouping.Min(cp => cp.Count), Max: grouping.Max(cp => cp.Count)));
        
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

public class DisplayingCompositions
{
    public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;
    private readonly Dictionary<(int id, ProductType productType), CompositionInfo> _items = new();
    private readonly object _lock = new();
    public IReadOnlyCollection<CompositionInfo> Items => _items.Values;

    public void AddOrIncrement(CompositionInfo info)
    {
        lock (_lock)
        {
            LastUpdated = DateTime.UtcNow;
            
            var key = (info.CompositionId, info.ProductType);

            if (_items.TryGetValue(key, out var existInfo))
            {
                existInfo.Count += info.Count;
            }
            else
            {
                _items[key] = info;
            }
        }
    }
}

public class ClaimData
{
    public List<ProductInfo> ProductInfos { get; set; } = new();
    public List<RandomProductInfo> RandomProductInfos { get; set; } = new();
    public List<CompositionInfo> CompositionInfos { get; set; } = new();
    public RewardPopupType RewardPopupType { get; set; }
}