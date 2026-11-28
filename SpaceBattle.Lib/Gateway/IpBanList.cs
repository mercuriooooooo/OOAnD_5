using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

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