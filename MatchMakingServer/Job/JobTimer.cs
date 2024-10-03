namespace MatchMakingServer.Job;

internal struct JobTimerElem
{
    public int ExecTick;
    public IJob Job;
}

public class JobTimer
{
    private readonly PriorityQueue<JobTimerElem, int> _priorityQueue = new();
    private readonly object _lock = new();

    public void Push(IJob job, int tickAfter = 0)
    {
        JobTimerElem jobElement;
        jobElement.ExecTick = Environment.TickCount + tickAfter;
        jobElement.Job = job;

        lock (_lock)
        {   // 내림차순 priority queue
            _priorityQueue.Enqueue(jobElement, -jobElement.ExecTick);
        }
    }

    public void Flush()
    {
        while (true)
        {
            int now = Environment.TickCount;
            JobTimerElem jobElement;
            
            lock (_lock)
            {
                if (_priorityQueue.Count == 0) break;
                jobElement = _priorityQueue.Peek();
                if (jobElement.ExecTick > now) break;
                
                _priorityQueue.Dequeue();
            }
            
            jobElement.Job.Execute();
        }
    }
}