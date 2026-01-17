using ApiServer.DB;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public class ExpiredTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredTokenCleanupService> _logger;

    private readonly TimeSpan _loopInterval = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _tokenCleanupInterval = TimeSpan.FromHours(2);
    // User before email verification
    private readonly TimeSpan _tempUserCleanupInterval = TimeSpan.FromMinutes(10);

    public ExpiredTokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<ExpiredTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredTokenCleanupService is starting.");

        var nextTokenCleanupUtc = DateTime.UtcNow;
        try
        {
            using var timer = new PeriodicTimer(_loopInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Clean up expired temp users every 10 minutes
                await CleanUpExpiredTempUsersAsync(stoppingToken);
                
                var nowUtc = DateTime.UtcNow;
                if (nowUtc >= nextTokenCleanupUtc)
                {
                    // Clean up expired tokens every 2 hours
                    await CleanUpExpiredTokensAsync(stoppingToken);
                    nextTokenCleanupUtc = nowUtc.Add(_tokenCleanupInterval);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 정상 종료
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExpiredTokenCleanupService crashed unexpectedly.");
        }
        finally
        {
            _logger.LogInformation("ExpiredTokenCleanupService is stopping.");
        }
    }

    private async Task CleanUpExpiredTokensAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nowUtc = DateTime.UtcNow;
        var expiredTokens = await context.RefreshToken
            .Where(token => token.ExpiresAt < nowUtc)
            .ToListAsync(stoppingToken);

        if (expiredTokens.Count == 0) return;
        
        context.RefreshToken.RemoveRange(expiredTokens);
        var affected = await context.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Removing {Count} expired refresh tokens.", affected);
    }

    private async Task CleanUpExpiredTempUsersAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 60분이 지난 임시 사용자 또는 이미 인증 완료된 사용자 삭제
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(60);
        var expiredUsers = await context.TempUser
            .Where(user => user.CreatedAt < cutoff || user.IsVerified)
            .ToListAsync(stoppingToken);

        if (expiredUsers.Count == 0) return;
        
        context.TempUser.RemoveRange(expiredUsers);
        var affected = await context.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Removing {Count} expired or verified temp users.", affected);
    }
}