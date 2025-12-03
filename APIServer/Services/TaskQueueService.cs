using System.Collections.Concurrent;

namespace ApiServer.Services;

public class TaskQueueService
{
    private readonly ConcurrentQueue<Func<Task>> _taskQueue = new();
    private readonly SemaphoreSlim _semaphore = new(1);
    
    public void Enqueue(Func<Task> task)
    {
        _taskQueue.Enqueue(task);
        ProcessQueue();
    }

    private async void ProcessQueue()
    {
        await _semaphore.WaitAsync(); // 단일 스레드에서 작업 처리 보장

        try
        {
            while (_taskQueue.TryDequeue(out var task))
            {
                await task();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}