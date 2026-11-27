using System.Collections.Concurrent;

namespace SpaceBattle.Lib;


public class RatingProfile
{
    public string Login { get; init; } = "";
    public int Pts { get; set; } = 1000;
    public int Wins { get; set; }
    public int Losses { get; set; }

    public override string ToString() => $"{Login}: {Pts} PTS (побед: {Wins}, поражений: {Losses})";
}


public class RatingService
{
    private const int K = 32;
    public const int DefaultPts = 1000;
    public const int RefusalPenalty = 10;
    public const int MaxRatingDifference = 200;

    private readonly ConcurrentDictionary<string, RatingProfile> _profiles = new();
    private readonly object _gate = new();

    public RatingProfile Get(string login) =>
        _profiles.GetOrAdd(login, _ => new RatingProfile { Login = login, Pts = DefaultPts });


    public void ApplyBattleResult(string winner, string loser)
    {
        lock (_gate)
        {
            var w = Get(winner);
            var l = Get(loser);
            var expectedWinner = 1.0 / (1.0 + Math.Pow(10, (l.Pts - w.Pts) / 400.0));
            var delta = Math.Max(1, (int)Math.Round(K * (1 - expectedWinner)));
            w.Pts += delta;
            w.Wins++;
            l.Pts = Math.Max(0, l.Pts - delta);
            l.Losses++;
        }
    }


    public void Penalize(string login)
    {
        var profile = Get(login);
        lock (_gate)
        {
            profile.Pts = Math.Max(0, profile.Pts - RefusalPenalty);
        }
    }


    public bool CanMatch(string a, string b) =>
        Math.Abs(Get(a).Pts - Get(b).Pts) <= MaxRatingDifference;


    public List<RatingProfile> Leaderboard()
    {
        lock (_gate)
        {
            return _profiles.Values.OrderByDescending(p => p.Pts).ToList();
        }
    }

    public IReadOnlyCollection<RatingProfile> Export() => _profiles.Values.ToList();
}
