namespace SpaceBattle.Lib;

public class GameScopeHierarchy
{
    public object Root { get; }
    public object CommonScope { get; }

    public GameScopeHierarchy(object root)
    {
        Root = root;
        CommonScope = ScopeContainer.Create(root, threadSafe: true);
    }

    public void RegisterShared(string dependency, Func<object[], object> strategy)
    {
        ScopeContainer.Register(CommonScope, dependency, strategy);
    }

    public object CreateGameScope()
    {
        var gameScope = ScopeContainer.Create(CommonScope, threadSafe: false);
        var gameItems = new Dictionary<string, object>();
        ScopeContainer.Register(gameScope, "Game.Items", _ => gameItems);
        return gameScope;
    }
}
