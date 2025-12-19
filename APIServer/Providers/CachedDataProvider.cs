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
    private readonly Dictionary<(UnitClass, int), int> _reinforcePoints;
    private readonly List<Product> _products;
    private readonly List<ProductComposition> _productCompositions;
    private readonly List<CompositionProbability> _probabilities;
    private readonly List<DailyProductSnapshot> _dailyProductSnapshots;
    private readonly List<FreeProductSnapshot> _freeProductSnapshots;
    private readonly Random _random = new();

    private readonly Dictionary<int, UnitInfo> _unitInfoLookup;
    private readonly Dictionary<int, SheepInfo> _sheepInfoLookup;
    private readonly Dictionary<int, EnchantInfo> _enchantInfoLookup;
    private readonly Dictionary<int, CharacterInfo> _characterInfoLookup;
    private readonly Dictionary<int, MaterialInfo> _materialLookup;
    private readonly Dictionary<int, List<OwnedMaterialInfo>> _unitMaterialLookup;
    private readonly Dictionary<int, List<Product>> _productLookup;
    private readonly Dictionary<int, List<ProductComposition>> _compLookup;
    private readonly Dictionary<int, List<CompositionProbability>> _probLookup;
    private readonly Dictionary<(int ProductId, int CompositionId), (int min, int max)> _probDetailLookup;
    
    public int QueueCountsSheep { get; set; } = 0;
    public int QueueCountsWolf { get; set; } = 0;
    
    public Dictionary<int, int> GetExpSnapshots() => _expSnapshots;
    public Dictionary<(UnitClass, int), int> GetReinforcePoints() => _reinforcePoints;
    public List<Product> GetProducts() => _products;
    public List<ProductComposition> GetProductCompositions() => _productCompositions;
    public List<CompositionProbability> GetProbabilities() => _probabilities;
    public IReadOnlyDictionary<int, UnitInfo> GetUnitLookup() => _unitInfoLookup;
    public IReadOnlyDictionary<int, SheepInfo> GetSheepLookup() => _sheepInfoLookup;
    public IReadOnlyDictionary<int, EnchantInfo> GetEnchantLookup() => _enchantInfoLookup;
    public IReadOnlyDictionary<int, CharacterInfo> GetCharacterLookup() => _characterInfoLookup;
    public IReadOnlyDictionary<int, MaterialInfo> GetMaterialLookup() => _materialLookup;
    public IReadOnlyDictionary<int, List<OwnedMaterialInfo>> GetUnitMaterialLookup() => _unitMaterialLookup;
    public IReadOnlyDictionary<int, List<Product>> GetProductLookup() => _productLookup;
    public IReadOnlyDictionary<int, List<ProductComposition>> GetCompLookup() => _compLookup;
    public IReadOnlyDictionary<int, List<CompositionProbability>> GetProbLookup() => _probLookup;
    public IReadOnlyDictionary<(int ProductId, int CompositionId), (int min, int max)> GetProbabilityLookup() => _probDetailLookup;
    public List<DailyProductSnapshot> GetDailyProductSnapshots() => _dailyProductSnapshots;
    public ConcurrentDictionary<int, DisplayingCompositions> DisplayingCompositions { get; } = new();
    
    public CachedDataProvider(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        using var context = dbContextFactory.CreateDbContext();
        
        _expSnapshots = context.Exp.AsNoTracking()
            .ToDictionary(exp => exp.Level, exp => exp.Exp);

        _reinforcePoints = context.ReinforcePoint.AsNoTracking()
            .ToDictionary(rp => (rp.Class, rp.Level), rp => rp.Constant);
        
        List<UnitInfo> unitInfos = context.Unit.AsNoTracking().Select(u => new UnitInfo
        {
            Id = (int)u.UnitId,
            Class = u.Class,
            Level = u.Level,
            Species = (int)u.Species,
            Role = u.Role,
            Faction = u.Faction,
            Region = u.Region
        }).ToList();
        
        List<SheepInfo> sheepInfos = context.Sheep.AsNoTracking().Select(s => new SheepInfo
        {
            Id = (int)s.SheepId,
            Class = s.Class
        }).ToList();
        
        List<EnchantInfo> enchantInfos = context.Enchant.AsNoTracking().Select(e => new EnchantInfo
        {
            Id = (int)e.EnchantId,
            Class = e.Class
        }).ToList();
        
        List<CharacterInfo> characterInfos = context.Character.AsNoTracking().Select(c => new CharacterInfo
        {
            Id = (int)c.CharacterId,
            Class = c.Class
        }).ToList();
        
        List<MaterialInfo> materialInfos = context.Material.AsNoTracking().Select(m => new MaterialInfo
        {
            Id = (int)m.MaterialId,
            Class = m.Class,
        }).ToList();
        
        List<UnitMaterial> unitMaterials = context.UnitMaterial.AsNoTracking().ToList();
        _products = context.Product.AsNoTracking().ToList();
        _productCompositions = context.ProductComposition.AsNoTracking().ToList();
        _probabilities = context.CompositionProbability.AsNoTracking().ToList();
        
        _unitInfoLookup = unitInfos.ToDictionary(u => u.Id, u => u);
        _sheepInfoLookup = sheepInfos.ToDictionary(s => s.Id, s => s);
        _enchantInfoLookup = enchantInfos.ToDictionary(e => e.Id, e => e);
        _characterInfoLookup = characterInfos.ToDictionary(c => c.Id, c => c);
        _materialLookup = materialInfos.ToDictionary(m => m.Id, m => m);
        
        _unitMaterialLookup = unitMaterials
            .GroupBy(um => um.UnitId)
            .ToDictionary(g => (int)g.Key,
                g => g
                .Select(um => new OwnedMaterialInfo
                {
                    MaterialInfo = _materialLookup[(int)um.MaterialId],
                    Count = um.Count
                })
                .ToList());
        
        _productLookup = _products
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        _compLookup = _productCompositions
            .GroupBy(pc => pc.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        _probLookup = _probabilities
            .GroupBy(cp => cp.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        _probDetailLookup = _probabilities
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
    
    public List<CompositionInfo> DrainDisplayingCompositions(int userId)
    {
        if (DisplayingCompositions.TryGetValue(userId, out var disp))
            return disp.Drain();

        return new List<CompositionInfo>();
    }
    
    public void ClearDisplayingCompositions(int userId)
    {
        DisplayingCompositions.TryRemove(userId, out _);
    }
}

public class DisplayingCompositions
{
    public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;
    private readonly Dictionary<(int id, ProductType productType), CompositionInfo> _items = new();
    private readonly object _lock = new();

    // ⚠️ 지금 Items는 lock 없이 Values를 노출합니다.
    // 가능하면 외부에서 Items를 직접 쓰지 말고, 아래 Snapshot/Drain만 쓰는 걸 권장합니다.
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
                _items[key] = info; // 필요하면 여기서 복사본 생성(불변성 강화)
            }
        }
    }

    public List<CompositionInfo> Drain()
    {
        lock (_lock)
        {
            // 스냅샷
            var list = _items.Values.ToList();
            // 비우기
            _items.Clear();
            LastUpdated = DateTime.UtcNow;
            return list;
        }
    }

    public List<CompositionInfo> Snapshot()
    {
        lock (_lock)
        {
            return _items.Values.ToList();
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