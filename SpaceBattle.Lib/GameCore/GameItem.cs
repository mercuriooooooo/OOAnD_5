namespace SpaceBattle.Lib;

public class GameItem : IGameItem
{
    private readonly Dictionary<string, object> _properties = new();

    public object GetProperty(string key)
    {
        if (!_properties.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"Property '{key}' was not found.");
        }

        return value;
    }

    public void SetProperty(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _properties[key] = value;
    }
}
