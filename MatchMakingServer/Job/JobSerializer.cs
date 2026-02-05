namespace MatchMakingServer.Job;

public class JobSerializer
{
    private readonly JobTimer _timer = new();
    private readonly Queue<IJob> _jobQueue = new();
    private readonly object _lock = new();

    protected bool HasJobs
    {
        get 
        { 
            lock (_lock)
            {
                return _jobQueue.Count > 0;
            }
        }
    }
    
    public void Push(Action action)
    {
        Push(new Job(action));
    }
    
    protected virtual void Push(IJob job)
    {
        lock (_lock)
        {
            _jobQueue.Enqueue(job);
        }
    }

    protected void Flush()
    {
        _timer.Flush();
        
        while (true)
        {
            IJob? job = Pop();
            if (job == null) return;
            
            job.Execute();
        }
    }

    private IJob? Pop()
    {
        lock (_lock)
        {
            return _jobQueue.Count == 0 ? null : _jobQueue.Dequeue();
        }
    }
}