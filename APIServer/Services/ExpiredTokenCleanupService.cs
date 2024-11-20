using ApiServer.DB;

namespace ApiServer.Services;

public class ExpiredTokenCleanupService : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExpiredTokenCleanupService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(CleanUpExpiredTokens, null, TimeSpan.Zero, TimeSpan.FromHours(2));
        _timer = new Timer(CleanUpExpiredTempUsers, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }

    private void CleanUpExpiredTokens(object? state)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var expiredTokens = dbContext.RefreshTokens.Where(token => token.ExpiresAt < now).ToList();
        if (expiredTokens.Any() == false) return;
        dbContext.RefreshTokens.RemoveRange(expiredTokens);
        dbContext.SaveChanges();
    }
    
    private void CleanUpExpiredTempUsers(object? state)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow + TimeSpan.FromMinutes(10);
        var expiredUsers = dbContext.TempUser.Where(user => user.CreatedAt < now || user.IsVerified).ToList();
        if (expiredUsers.Any() == false) return;
        dbContext.TempUser.RemoveRange(expiredUsers);
        dbContext.SaveChanges();
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _timer?.Dispose();
    }
}