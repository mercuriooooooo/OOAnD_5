using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

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