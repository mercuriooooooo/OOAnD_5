namespace SpaceBattle.Lib;


public class FairMatchmaker
{
    private readonly object _gate = new();
    private readonly Queue<string> _waiting = new();
    private readonly HashSet<string> _inQueue = new(StringComparer.OrdinalIgnoreCase);
    private readonly RatingService _rating;

    public FairMatchmaker(RatingService rating) => _rating = rating;

    public bool Enqueue(string login)
    {
        lock (_gate)
        {
            if (!_inQueue.Add(login))
                return false;
            _waiting.Enqueue(login);
            return true;
        }
    }


    public (string First, string Second)? TryPair()
    {
        lock (_gate)
        {
            if (_waiting.Count < 2)
                return null;

            var order = _waiting.ToArray();
            for (var i = 0; i < order.Length; i++)
            {
                for (var j = i + 1; j < order.Length; j++)
                {
                    if (!_rating.CanMatch(order[i], order[j]))
                        continue;
                    var chosen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { order[i], order[j] };
                    _waiting.Clear();
                    foreach (var rest in order.Where(x => !chosen.Contains(x)))
                        _waiting.Enqueue(rest);
                    foreach (var c in chosen)
                        _inQueue.Remove(c);
                    return (order[i], order[j]);
                }
            }
            return null;
        }
    }

    public int WaitingCount
    {
        get { lock (_gate) { return _waiting.Count; } }
    }
}
