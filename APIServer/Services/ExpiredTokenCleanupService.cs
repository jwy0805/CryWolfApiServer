using ApiServer.DB;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public class ExpiredTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredTokenCleanupService> _logger;

    public ExpiredTokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<ExpiredTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredTokenCleanupService is starting.");

        DateTime lastTokenCleanup = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. 임시 사용자 정리 (5분마다)
                await CleanUpExpiredTempUsersAsync(stoppingToken);

                // 2. 만료된 토큰 정리 (2시간마다)
                if (DateTime.UtcNow - lastTokenCleanup >= TimeSpan.FromHours(2))
                {
                    await CleanUpExpiredTokensAsync(stoppingToken);
                    lastTokenCleanup = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                // 서비스 중지 시 발생하는 정상적인 상황
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during expired token/user cleanup.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 서비스 중지 시 루프 종료
            }
        }

        _logger.LogInformation("ExpiredTokenCleanupService is stopping.");
    }

    private async Task CleanUpExpiredTokensAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var now = DateTime.UtcNow;
        var expiredTokens = await dbContext.RefreshTokens
            .Where(token => token.ExpiresAt < now)
            .ToListAsync(stoppingToken);

        if (expiredTokens.Any())
        {
            _logger.LogInformation("Removing {Count} expired refresh tokens.", expiredTokens.Count);
            dbContext.RefreshTokens.RemoveRange(expiredTokens);
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }

    private async Task CleanUpExpiredTempUsersAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // 10분이 지난 임시 사용자 또는 이미 인증 완료된 사용자 삭제
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        var expiredUsers = await dbContext.TempUser
            .Where(user => user.CreatedAt < cutoff || user.IsVerified)
            .ToListAsync(stoppingToken);

        if (expiredUsers.Any())
        {
            _logger.LogInformation("Removing {Count} expired or verified temp users.", expiredUsers.Count);
            dbContext.TempUser.RemoveRange(expiredUsers);
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}