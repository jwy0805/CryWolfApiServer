namespace ApiServer.Services;

public class DailyJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyJob> _logger;

    public DailyJob(IServiceProvider serviceProvider, ILogger<DailyJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Waiting until UTC 00:00:00 while the data is being updated
            var next = DateTime.UtcNow.Date.AddDays(1);
            var delay = next - DateTime.UtcNow;
            await Task.Delay(delay, stoppingToken);
            await RunMidnightJob(stoppingToken);
            _logger.LogInformation("Daily job executed at {Time}", DateTime.UtcNow);
        }
    }

    private async Task RunMidnightJob(CancellationToken token)
    {
        await ResetDailyProducts(token);
    }

    private async Task ResetDailyProducts(CancellationToken token)
    {
        Console.WriteLine("Resetting daily products...");
        using var scope = _serviceProvider.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IDailyProductService>();
        await provider.SnapshotDailyProductsAsync(DateOnly.FromDateTime(DateTime.UtcNow), token);
    }
}