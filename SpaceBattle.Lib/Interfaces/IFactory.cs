namespace SpaceBattle.Lib;

public interface IFactory
{
    object Create(IGameItem item);
}
