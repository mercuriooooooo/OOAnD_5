namespace SpaceBattle.Lib;

public interface IGameItem
{
    object GetProperty(string key);
    void SetProperty(string key, object value);
}
