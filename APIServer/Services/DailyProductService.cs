using ApiServer.DB;
using ApiServer.Providers;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public interface IDailyProductService
{
    Task SnapshotDailyProductsAsync(DateOnly dateOnly, CancellationToken token = default);
    Task CreateUserDailyProductSnapshotAsync(int userId, DateOnly dateOnly, byte refreshIndex,
        CancellationToken token = default);
    Task<bool> RefreshByAdsAsync(int userId, CancellationToken token = default);
    Task<List<DailyProductInfo>> GetDailyProductInfos(
        int userId,
        List<Product> products,
        List<ProductComposition> compositions,
        List<CompositionProbability> probabilities);
}

public class DailyProductService : IDailyProductService
{
    private readonly AppDbContext _context;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<DailyProductService> _logger;
    
    public DailyProductService(
        AppDbContext dbContext, CachedDataProvider cachedDataProvider, ILogger<DailyProductService> logger)
    {
        _context = dbContext;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }

    public async Task SnapshotDailyProductsAsync(DateOnly dateOnly, CancellationToken token = default)
    {
        var userIds = await _context.User.Select(user => user.UserId).ToListAsync(token);
        
        foreach (var userId in userIds)
        {
            await CreateUserDailyProductSnapshotAsync(userId, dateOnly, 0, token);
        }

        _logger.LogInformation("DailyProducts snapshot for {Count} done.", userIds.Count);
    }

    public async Task<bool> RefreshByAdsAsync(int userId, CancellationToken token = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snaps = await _context.UserDailyProduct
            .Where(udp => udp.UserId == userId).ToListAsync(token);

        if (snaps.Count == 0) return false;
        if (DateTime.UtcNow - snaps[0].RefreshAt < TimeSpan.FromHours(6)) return false;
        
        var newIndex = (byte)(snaps[0].RefreshIndex + 1);
        _context.UserDailyProduct.RemoveRange(snaps);
        
        await CreateUserDailyProductSnapshotAsync(userId, today, newIndex, token);
        return true;
    }
    
    public async Task CreateUserDailyProductSnapshotAsync(int userId, DateOnly dateOnly, byte refreshIndex,
        CancellationToken token)
    {
        // Daily products that can be purchased without watching ads (3 slots)
        var openPicks = _cachedDataProvider.GetRandomDailyProductsDistinct(3);
        // Daily products that can be purchased by watching ads (3 slots - 1 is free, 2 are paid)
        var freePick = _cachedDataProvider.GetRandomFreeProduct();
        var closedPicks = _cachedDataProvider.GetRandomDailyProductsForClosedPicks(2);
        
        var dailyProductIds = openPicks.Concat(new [] { freePick }).Concat(closedPicks).ToList();
        var slot = 0;

        var userDailyProducts = dailyProductIds
            .Select(productId => new UserDailyProduct
            {
                UserId = userId,
                Slot = (byte)slot++,
                ProductId = productId,
                SeedDate = dateOnly,
                RefreshIndex = refreshIndex,
                RefreshAt = DateTime.UtcNow,
                Bought = false,
                NeedAds = slot > 3,
                AdsWatched = false,
            })
            .ToList();
        
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(token);
            try
            {
                var existing = _context.UserDailyProduct.Where(udp => udp.UserId == userId);
                _context.UserDailyProduct.RemoveRange(existing);
                _context.UserDailyProduct.AddRange(userDailyProducts);
                await _context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                Console.WriteLine($"New free daily item {freePick}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });
    }

    public async Task<List<DailyProductInfo>> GetDailyProductInfos(
        int userId,
        List<Product> products,
        List<ProductComposition> compositions,
        List<CompositionProbability> probabilities)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userDailyProducts = await _context.UserDailyProduct.AsNoTracking()
            .Where(udp => udp.UserId == userId && udp.SeedDate == today)
            .OrderBy(udp => udp.Slot)
            .ToListAsync();

        return userDailyProducts.Select(udp =>
        {
            var product = products.FirstOrDefault(p => p.ProductId == udp.ProductId) ?? new Product();
            var productInfo = new ProductInfo
            {
                Id = product.ProductId,
                Price = _cachedDataProvider.GetDailyProductPrice(product.ProductId),
                CurrencyType = product.Currency,
                Category = product.Category,
                ProductCode = product.ProductCode,
                Compositions = compositions.Where(pc => pc.ProductId == product.ProductId)
                    .Select(pc => new CompositionInfo
                    {
                        Id = pc.ProductId,
                        CompositionId = pc.CompositionId,
                        Type = pc.Type,
                        Count = pc.Count,
                        MinCount = pc is { Count: 0, Guaranteed: false }
                            ? probabilities
                                .Where(cp => cp.ProductId == pc.ProductId && cp.CompositionId == pc.CompositionId)
                                .Min(cp => cp.Count)
                            : 0,
                        MaxCount = pc is { Count: 0, Guaranteed: false }
                            ? probabilities
                                .Where(cp => cp.ProductId == pc.ProductId && cp.CompositionId == pc.CompositionId)
                                .Max(cp => cp.Count)
                            : 0,
                        Guaranteed = pc.Guaranteed,
                        IsSelectable = pc.IsSelectable
                    }).ToList(),
            };
            return new DailyProductInfo
            {
                ProductInfo = productInfo,
                Class = _cachedDataProvider.GetDailyProductSnapshots()
                    .First(record => record.ProductId == udp.ProductId).Class,
                Slot = udp.Slot,
                Bought = udp.Bought,
                AdsWatched = udp.AdsWatched,
                NeedAds = udp.NeedAds
            };
        }).ToList();
    }
}