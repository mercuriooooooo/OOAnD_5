using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public class ServerQueue : IServerQueue
{
    private readonly ConcurrentQueue<ICommand> _queue = new();
    private readonly int _maxCapacity;

    public string ServerId { get; }
    public int MaxCapacity => _maxCapacity;

    public ServerQueue(string serverId, int maxCapacity = 1000)
    {
        ServerId = serverId;
        _maxCapacity = maxCapacity;
    }

    public void Enqueue(ICommand command) => _queue.Enqueue(command);

    public bool TryEnqueue(ICommand command)
    {
        if (_queue.Count >= _maxCapacity)
            return false;
        _queue.Enqueue(command);
        return true;
    }

    public bool TryDequeue(out ICommand command) => _queue.TryDequeue(out command);
    public int Count => _queue.Count;

    public ICommand[] Drain()
    {
        return _queue.ToArray();
    }
}