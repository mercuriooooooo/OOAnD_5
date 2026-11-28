using System.Collections.Concurrent;
using System.Text.Json;

namespace SpaceBattle.Lib;


public class BattleEvent
{
    public int Tick { get; set; }
    public string Text { get; set; } = "";
}


public class BattleRecord
{
    public string GameId { get; set; } = "";
    public string Winner { get; set; } = "";
    public string Loser { get; set; } = "";
    public int Ticks { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public List<BattleEvent> Events { get; set; } = new();
}

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<T> Load<T>(string path)
    {
        if (!File.Exists(path)) return new List<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    public static void Save<T>(string path, List<T> items)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(items, Options));
    }
}


public class BattleHistoryService
{
    private readonly ConcurrentQueue<BattleRecord> _records = new();
    private readonly object _fileGate = new();
    private readonly string _path;

    public BattleHistoryService(string path)
    {
        _path = path;
        foreach (var record in JsonStore.Load<BattleRecord>(path))
            _records.Enqueue(record);
    }

    public void Append(BattleRecord record)
    {
        _records.Enqueue(record);
        lock (_fileGate)
        {
            var all = JsonStore.Load<BattleRecord>(_path);
            all.Add(record);
            JsonStore.Save(_path, all);
        }
    }

    public List<BattleRecord> All() => _records.ToList();


    public List<BattleRecord> ForPlayer(string login) =>
        _records.Where(r => r.Winner.Equals(login, StringComparison.OrdinalIgnoreCase)
                         || r.Loser.Equals(login, StringComparison.OrdinalIgnoreCase)).ToList();


    public (int Wins, int Losses) Stats(string login)
    {
        var games = ForPlayer(login);
        return (games.Count(g => g.Winner.Equals(login, StringComparison.OrdinalIgnoreCase)),
                games.Count(g => g.Loser.Equals(login, StringComparison.OrdinalIgnoreCase)));
    }
}
