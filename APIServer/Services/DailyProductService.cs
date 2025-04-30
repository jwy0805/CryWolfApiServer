using ApiServer.DB;
using ApiServer.Providers;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public interface IDailyProductService
{
    Task SnapshotDailyProductsAsync(DateOnly dateOnly, CancellationToken token = default);
    Task<bool> RefreshByAdsAsync(int userId, CancellationToken token = default);
}

public class DailyProductService : IDailyProductService
{
    private readonly AppDbContext _dbContext;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<DailyProductService> _logger;
    private readonly Random _random = new();
    
    public DailyProductService(
        AppDbContext dbContext, CachedDataProvider cachedDataProvider, ILogger<DailyProductService> logger)
    {
        _dbContext = dbContext;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }

    public async Task SnapshotDailyProductsAsync(DateOnly dateOnly, CancellationToken token = default)
    {
        var userIds = await _dbContext.User.Select(user => user.UserId).ToListAsync(token);
        var dailyProductSet = await _dbContext.DailyProduct.AsNoTracking().ToListAsync(token);

        foreach (var userId in userIds)
        {
            await CreateDailyProductSnapshotAsync(userId, dateOnly, 0, dailyProductSet, token);
        }

        await _dbContext.SaveChangesAsync(token);
        _logger.LogInformation("DailyProducts snapshot for {Count} done.", userIds.Count);
    }

    public async Task<bool> RefreshByAdsAsync(int userId, CancellationToken token = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var snaps = await _dbContext.UserDailyProduct
            .Where(udp => udp.UserId == userId && udp.SeedDate == today).ToListAsync(token);

        if (snaps.Count == 0) return false;
        if (DateTime.UtcNow - snaps[0].RefreshAt < TimeSpan.FromHours(6)) return false;
        
        var dailySet = await _dbContext.DailyProduct.AsNoTracking().ToListAsync(token);
        var newIndex = (byte)(snaps[0].RefreshIndex + 1);
        _dbContext.UserDailyProduct.RemoveRange(snaps);
        
        await CreateDailyProductSnapshotAsync(userId, today, newIndex, dailySet, token);
        await _dbContext.SaveChangesAsync(token);
        return true;
    }
    
    private async Task CreateDailyProductSnapshotAsync(int userId, DateOnly dateOnly, byte refreshIndex,
        List<DailyProduct> dailySet, CancellationToken token)
    {
        // Daily products that can be purchased without watching ads (3 slots)
        var openPicks = _cachedDataProvider.GetRandomDailyProductsDistinct(3);
        // Daily products that can be purchased by watching ads (3 slots - 1 is free, 2 are paid)
        var freePick = _cachedDataProvider.GetRandomFreeProduct();
        // var closedPicks = _cachedDataProvider.GetRandomDailyProductsForClosedPicks(2);
        var closedPicks = _cachedDataProvider.GetRandomDailyProductsDistinct(2);
        
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
            })
            .ToList();
    }
}