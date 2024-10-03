using MatchMakingServer.Job;
// ReSharper disable ClassNeverInstantiated.Global

namespace AccountServer.Services;

public class JobService : JobSerializer, IHostedService
{
    private Timer? _timer;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(FlushJobs, null, TimeSpan.Zero, TimeSpan.FromSeconds(0.1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }
    
    private void FlushJobs(object? state)
    {
        Flush();
    }
}