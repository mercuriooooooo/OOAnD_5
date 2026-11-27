using System.Collections.Concurrent;
using App;
using App.Scopes;

namespace SpaceBattle.Lib.Tests;

public class ScopeTests : IDisposable
{
    public ScopeTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Clear").Execute();
        new RegisterIoCDependencyScopes().Execute();
    }

    public void Dispose()
    {
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Clear").Execute();
    }

    [Fact]
    public void ScopesCurrent_ReturnsScopeSetForThisThread()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var scope = Ioc.Resolve<object>("Scopes.Create", root);

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", scope).Execute();

        Assert.Same(scope, Ioc.Resolve<object>("Scopes.Current"));
        Assert.Same(scope, Ioc.Resolve<object>("IoC.Scope.Current"));
    }

    [Fact]
    public void CurrentScope_IsIsolatedBetweenThreads()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var scopeA = ScopeContainer.Create(root);
        var scopeB = ScopeContainer.Create(root);
        ScopeContainer.Register(scopeA, "Movement", _ => "A");
        ScopeContainer.Register(scopeB, "Movement", _ => "B");

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", scopeA).Execute();

        string? seenOnOtherThread = null;
        var thread = new Thread(() =>
        {
            Ioc.Resolve<App.ICommand>("Scopes.Current.Set", scopeB).Execute();
            seenOnOtherThread = Ioc.Resolve<string>("Movement");
        });
        thread.Start();
        thread.Join();

        Assert.Equal("B", seenOnOtherThread);
        Assert.Equal("A", Ioc.Resolve<string>("Movement"));
        Assert.Same(scopeA, Ioc.Resolve<object>("Scopes.Current"));
    }

    [Fact]
    public void InSeparateScopeCommand_ExecutesInTargetScopeAndRestoresPrevious()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var outer = ScopeContainer.Create(root);
        var inner = ScopeContainer.Create(root);
        ScopeContainer.Register(outer, "Movement", _ => "outer");
        ScopeContainer.Register(inner, "Movement", _ => "inner");

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", outer).Execute();

        string? seen = null;
        var command = new InSeparateScopeCommand(
            new DelegateCommand(() => seen = Ioc.Resolve<string>("Movement")),
            inner);

        command.Execute();

        Assert.Equal("inner", seen);
        Assert.Same(outer, Ioc.Resolve<object>("Scopes.Current"));
        Assert.Equal("outer", Ioc.Resolve<string>("Movement"));
    }

    [Fact]
    public void InSeparateScopeCommand_RestoresScopeWhenCommandThrows()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var outer = ScopeContainer.Create(root);
        var inner = ScopeContainer.Create(root);

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", outer).Execute();

        var command = new InSeparateScopeCommand(
            new DelegateCommand(() => throw new InvalidOperationException("boom")),
            inner);

        var exception = Assert.Throws<InvalidOperationException>(() => command.Execute());

        Assert.Equal("boom", exception.Message);
        Assert.Same(outer, Ioc.Resolve<object>("Scopes.Current"));
    }

    [Fact]
    public void InSeparateScopeCommand_KeepsRegistrationInsideTargetScope()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var outer = ScopeContainer.Create(root);
        var inner = ScopeContainer.Create(root);

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", outer).Execute();

        new InSeparateScopeCommand(new DelegateCommand(() =>
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", "OnlyInner", (object[] _) => "inner-value").Execute();
        }), inner).Execute();

        Assert.ThrowsAny<Exception>(() => Ioc.Resolve<string>("OnlyInner"));

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", inner).Execute();
        Assert.Equal("inner-value", Ioc.Resolve<string>("OnlyInner"));
    }

    [Fact]
    public void GameScopes_InheritSharedCommandsAndIsolateGameItems()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var hierarchy = new GameScopeHierarchy(root);
        hierarchy.RegisterShared("Commands.Move", _ => "shared-move");
        hierarchy.RegisterShared("Commands.Rotate", _ => "shared-rotate");

        var game1 = hierarchy.CreateGameScope();
        var game2 = hierarchy.CreateGameScope();
        ScopeContainer.Register(game1, "Movement", _ => "game-1");
        ScopeContainer.Register(game1, "Game1Only", _ => "secret");
        ScopeContainer.Register(game2, "Movement", _ => "game-2");

        var rootStorage = (IDictionary<string, Func<object[], object>>)root;
        Assert.False(rootStorage.ContainsKey("Commands.Move"));
        Assert.False(rootStorage.ContainsKey("Commands.Rotate"));
        Assert.False(rootStorage.ContainsKey("Game.Items"));
        Assert.IsType<ConcurrentDictionary<string, Func<object[], object>>>(hierarchy.CommonScope);
        Assert.IsType<Dictionary<string, Func<object[], object>>>(game1);

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", game1).Execute();
        var items1 = Ioc.Resolve<IDictionary<string, object>>("Game.Items");
        items1["ship"] = "alpha";

        Assert.Equal("game-1", Ioc.Resolve<string>("Movement"));
        Assert.Equal("shared-move", Ioc.Resolve<string>("Commands.Move"));
        Assert.Equal("shared-rotate", Ioc.Resolve<string>("Commands.Rotate"));

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", game2).Execute();
        var items2 = Ioc.Resolve<IDictionary<string, object>>("Game.Items");

        Assert.Equal("game-2", Ioc.Resolve<string>("Movement"));
        Assert.Equal("shared-move", Ioc.Resolve<string>("Commands.Move"));
        Assert.NotSame(items1, items2);
        Assert.False(items2.ContainsKey("ship"));
        Assert.ThrowsAny<Exception>(() => Ioc.Resolve<string>("Game1Only"));
    }

    [Fact]
    public void GameScope_OverridesSharedDependency()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var hierarchy = new GameScopeHierarchy(root);
        hierarchy.RegisterShared("Movement", _ => "shared");

        var game1 = hierarchy.CreateGameScope();
        var game2 = hierarchy.CreateGameScope();
        ScopeContainer.Register(game1, "Movement", _ => "custom");

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", game1).Execute();
        Assert.Equal("custom", Ioc.Resolve<string>("Movement"));

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", game2).Execute();
        Assert.Equal("shared", Ioc.Resolve<string>("Movement"));
    }

    [Fact]
    public void ThreadSafeCommonScope_AcceptsParallelRegistrations()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var common = Ioc.Resolve<IDictionary<string, Func<object[], object>>>("Scopes.Create.ThreadSafe", root);
        var errors = 0;

        Parallel.For(0, 100, i =>
        {
            try
            {
                ScopeContainer.Register(common, "Cmd." + i, _ => i);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        });

        Assert.Equal(0, errors);
        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", common).Execute();
        Assert.Equal(7, Ioc.Resolve<int>("Cmd.7"));
        Assert.Equal(99, Ioc.Resolve<int>("Cmd.99"));
    }

    [Fact]
    public void Create_WithoutThreadSafeFlag_ReturnsPlainDictionary()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var scope = Ioc.Resolve<object>("Scopes.Create", root);

        Assert.IsType<Dictionary<string, Func<object[], object>>>(scope);
    }

    [Fact]
    public void GameExecute_ResolvesDependenciesFromItsOwnScope()
    {
        var root = Ioc.Resolve<object>("Scopes.Current");
        var hierarchy = new GameScopeHierarchy(root);
        var game1 = hierarchy.CreateGameScope();
        var game2 = hierarchy.CreateGameScope();
        ScopeContainer.Register(game1, "Movement", _ => "game-1");
        ScopeContainer.Register(game2, "Movement", _ => "game-2");

        var queue = new Queue<SpaceBattle.Lib.ICommand>();
        string? seen = null;
        queue.Enqueue(new DelegateCommand(() => seen = Ioc.Resolve<string>("Movement")));
        ScopeContainer.Register(game1, "Game.CommandsQueue", _ => queue);
        ScopeContainer.Register(game1, "Game.TimeQuant", _ => TimeSpan.FromMilliseconds(100));

        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", game2).Execute();
        new Game(game1).Execute();

        Assert.Equal("game-1", seen);
        Assert.Same(game1, Ioc.Resolve<object>("Scopes.Current"));
    }
}
