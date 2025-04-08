using ApiServer.DB;
using Exception = System.Exception;

namespace ApiServer.Services;

public class UserManagementService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserManagementService> _logger;
    private readonly TimeSpan _inactiveThreshold = TimeSpan.FromSeconds(120);
    private const int CheckInterval = 60;
    
    public UserManagementService(IServiceProvider serviceProvider, ILogger<UserManagementService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var thresholdTime = now - _inactiveThreshold;
                var inactiveUsers = dbContext.User
                    .Where(u => u.LastPingTime < thresholdTime && u.Act != UserAct.Offline)
                    .ToList();
                
                foreach (var user in inactiveUsers)
                {
                    user.Act = UserAct.Offline;
                    _logger.LogInformation($"[UserManagementService] Set user {user.UserName} to offline (last ping: {user.LastPingTime})");
                }
                
                if (inactiveUsers.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[UserManagementService] Error occurred while checking inactive users");
            }
            
            await Task.Delay(CheckInterval * 1000, stoppingToken);
        }
    }
}