using System.Security.Cryptography;
using System.Text;

namespace SpaceBattle.Lib;

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