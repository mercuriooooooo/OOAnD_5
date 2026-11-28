namespace SpaceBattle.Lib;

public interface IGameRule
{
    string Name { get; }
    string CommandTypeName { get; }
    string DependencyKey { get; }
    string Parameters { get; }
    string Describe();
}
