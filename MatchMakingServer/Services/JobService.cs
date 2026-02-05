using MatchMakingServer.Job;
// ReSharper disable ClassNeverInstantiated.Global

namespace AccountServer.Services;

public class JobService : JobSerializer, IHostedService
{
    private readonly ILogger<JobService> _logger;
    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _signaled;

    public JobService(ILogger<JobService> logger)
    {
        _logger = logger;
    }
    
    protected override void Push(IJob job)
    {
        base.Push(job);
        NotifyWorker();
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = Task.Run(() => WorkerLoop(_cts.Token), CancellationToken.None);
        _logger.LogInformation("Job service started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _signal.Release();
        
        if (_worker != null)
        {
            await Task.WhenAny(_worker, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
    
    private void NotifyWorker()
    {
        if (Interlocked.Exchange(ref _signaled, 1) == 0) _signal.Release();
    }
    
    private async Task WorkerLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await _signal.WaitAsync(token);
                
                // 다음 Push가 다시 깨울 수 있게
                Interlocked.Exchange(ref _signaled, 0);
                // 단일 스레드 Flush
                Flush();

                if (HasJobs && Interlocked.Exchange(ref _signaled, 1) == 0)
                {
                    _signal.Release();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            _logger.LogCritical(e, "JobService worker crashed.");
            throw;
        }
    }
}