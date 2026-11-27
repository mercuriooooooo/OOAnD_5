using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpaceBattle.Lib;


public class IncomingMessage
{
    public string GameId { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Type { get; set; } = "";
    public string PayloadJson { get; set; } = "";
}


public interface IQueueWriter
{
    bool TryEnqueue(ICommand command);
}


public interface IQueueReader
{
    bool TryDequeue(out ICommand command);
    int Count { get; }
}


public interface IServerQueue : IQueueWriter, IQueueReader
{
    string ServerId { get; }
    void Enqueue(ICommand command);
    int Count { get; }
}

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
    public ICommand[] Drain() { var arr = _queue.ToArray(); return arr; }
}


public class ReceiveMessageCommand : ICommand
{
    public IncomingMessage Message { get; }
    public ReceiveMessageCommand(IncomingMessage message) => Message = message;
    public void Execute() {   }
}


public class RateLimiter
{
    private class Bucket
    {
        public double Tokens;
        public DateTime LastRefill;
    }

    private readonly int _capacity;
    private readonly int _refillPerSecond;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    public RateLimiter(int capacity, int refillPerSecond)
    {
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
    }

    public bool TryAcquire(string ip)
    {
        var bucket = _buckets.GetOrAdd(ip, _ => new Bucket { Tokens = _capacity, LastRefill = DateTime.UtcNow });
        lock (bucket)
        {
            var now = DateTime.UtcNow;
            bucket.Tokens = Math.Min(_capacity, bucket.Tokens + (now - bucket.LastRefill).TotalSeconds * _refillPerSecond);
            bucket.LastRefill = now;
            if (bucket.Tokens >= 1)
            {
                bucket.Tokens--;
                return true;
            }
            return false;
        }
    }
}


public class IpBanList
{
    private readonly ConcurrentDictionary<string, DateTime> _bans = new();

    public void Ban(string ip, TimeSpan duration) => _bans[ip] = DateTime.UtcNow + duration;

    public bool IsBanned(string ip)
    {
        if (!_bans.TryGetValue(ip, out var until)) return false;
        if (until > DateTime.UtcNow) return true;
        _bans.TryRemove(ip, out _);
        return false;
    }
}


public class ConsistentHashRouter
{
    private readonly SortedDictionary<int, string> _ring = new();
    private readonly int _virtualNodes;
    private readonly object _gate = new();

    public ConsistentHashRouter(int virtualNodes = 32) => _virtualNodes = virtualNodes;

    public void AddServer(string serverId)
    {
        lock (_gate)
        {
            for (var i = 0; i < _virtualNodes; i++)
                _ring[Hash(serverId + ":" + i)] = serverId;
        }
    }

    public void RemoveServer(string serverId)
    {
        lock (_gate)
        {
            foreach (var key in _ring.Where(kv => kv.Value == serverId).Select(kv => kv.Key).ToList())
                _ring.Remove(key);
        }
    }

    public string Route(string key)
    {
        lock (_gate)
        {
            if (_ring.Count == 0)
                throw new InvalidOperationException("Кольцо хэширования пусто — нет серверов");
            var h = Hash(key);
            foreach (var kv in _ring)
                if (kv.Key >= h)
                    return kv.Value;
            return _ring.First().Value;
        }
    }

    private static int Hash(string s)
    {
        using var md5 = MD5.Create();
        return BitConverter.ToInt32(md5.ComputeHash(Encoding.UTF8.GetBytes(s)));
    }
}


public class EndPointProxy
{
    private readonly RateLimiter _rateLimiter;
    private readonly IpBanList _banList;
    private readonly ConsistentHashRouter _router;
    private readonly IReadOnlyDictionary<string, IServerQueue> _servers;

    public EndPointProxy(RateLimiter rateLimiter, IpBanList banList,
        ConsistentHashRouter router, IReadOnlyDictionary<string, IServerQueue> servers)
    {
        _rateLimiter = rateLimiter;
        _banList = banList;
        _router = router;
        _servers = servers;
    }

    public string Handle(string json)
    {
        IncomingMessage message;
        try
        {
            message = JsonSerializer.Deserialize<IncomingMessage>(json)
                      ?? throw new JsonException("пустое сообщение");
        }
        catch (JsonException e)
        {
            return $"REJECTED: некорректный JSON ({e.Message})";
        }

        if (string.IsNullOrEmpty(message.GameId))
            return "REJECTED: сообщение без GameId";

        if (_banList.IsBanned(message.Ip))
            return $"REJECTED: IP '{message.Ip}' заблокирован";

        if (!_rateLimiter.TryAcquire(message.Ip))
        {
            _banList.Ban(message.Ip, TimeSpan.FromSeconds(30));
            return $"REJECTED: аномальная активность с '{message.Ip}' — лимит частоты превышен, IP заблокирован";
        }

        var serverId = _router.Route(message.GameId);
        _servers[serverId].Enqueue(new ReceiveMessageCommand(message));
        return $"OK: игра '{message.GameId}' -> сервер '{serverId}'";
    }
}
