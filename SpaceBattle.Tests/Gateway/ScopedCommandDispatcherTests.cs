using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Lib.Tests;

public class ScopedCommandDispatcherTests
{
    private sealed class ScopeSwitch : ICommand
    {
        [ThreadStatic] public static string Current = "root";

        private readonly string _scopeName;
        private readonly ICommand _inner;
        private readonly List<string> _log;

        public ScopeSwitch(string scopeName, ICommand inner, List<string> log)
        {
            _scopeName = scopeName;
            _inner = inner;
            _log = log;
        }

        public void Execute()
        {
            _log.Add($"enter:{_scopeName}");
            var old = Current;
            Current = _scopeName;
            try
            {
                _inner.Execute();
            }
            finally
            {
                Current = old;
                _log.Add($"leave:{_scopeName}");
            }
        }
    }

    private static Func<ICommand, ICommand> Wrapper(string scope, List<string> log) =>
        command => new ScopeSwitch(scope, command, log);

    [Fact]
    public void ProcessNext_WrapsCommandBeforeExecution()
    {
        var log = new List<string>();
        var queue = new ServerQueue("srv", maxCapacity: 10);
        queue.Enqueue(new DelegateCommand(() => log.Add($"run:{ScopeSwitch.Current}")));

        var dispatcher = new ScopedCommandDispatcher(queue, Wrapper("game-1", log));

        Assert.True(dispatcher.ProcessNext());
        Assert.Equal(new[] { "enter:game-1", "run:game-1", "leave:game-1" }, log);
        Assert.Equal("root", ScopeSwitch.Current);
        Assert.Equal(1, dispatcher.Processed);
    }

    [Fact]
    public void ProcessNext_EmptyQueue_ReturnsFalse()
    {
        var dispatcher = new ScopedCommandDispatcher(
            new ServerQueue("srv", 5), Wrapper("game-1", new List<string>()));

        Assert.False(dispatcher.ProcessNext());
        Assert.Equal(0, dispatcher.Processed);
    }

    [Fact]
    public void ProcessAvailable_DrainsQueueInFifoOrder()
    {
        var log = new List<string>();
        var queue = new ServerQueue("srv", maxCapacity: 10);
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            queue.Enqueue(new DelegateCommand(() => log.Add($"run:{index}")));
        }

        var dispatcher = new ScopedCommandDispatcher(queue, command => command);

        Assert.Equal(5, dispatcher.ProcessAvailable());
        Assert.Equal(new[] { "run:0", "run:1", "run:2", "run:3", "run:4" }, log);
        Assert.Equal(0, queue.Count);
        Assert.Equal(5, dispatcher.Processed);
    }

    [Fact]
    public void ProcessNext_CommandFails_ReportedToProblemSolver_ProcessingContinues()
    {
        var queue = new ServerQueue("srv", maxCapacity: 10);
        var executed = 0;
        queue.Enqueue(new DelegateCommand(() => throw new InvalidOperationException("сбой команды")));
        queue.Enqueue(new DelegateCommand(() => executed++));

        using var problems = new ProblemSolverThread();
        ProblemReport? report = null;
        var signal = new ManualResetEventSlim();
        problems.ProblemLogged += r => { report = r; signal.Set(); };

        var dispatcher = new ScopedCommandDispatcher(queue, command => command, problems);

        Assert.True(dispatcher.ProcessNext());
        Assert.True(dispatcher.ProcessNext());
        Assert.True(signal.Wait(2000));

        Assert.IsType<InvalidOperationException>(report!.Exception);
        Assert.Equal(1, executed);
        Assert.Equal(1, dispatcher.Processed);
    }

    [Fact]
    public void TwoGames_UseTheirOwnScopes()
    {
        var log = new List<string>();
        var queue = new ServerQueue("srv", maxCapacity: 10);
        queue.Enqueue(new DelegateCommand(() => log.Add(ScopeSwitch.Current)));
        queue.Enqueue(new DelegateCommand(() => log.Add(ScopeSwitch.Current)));

        new ScopedCommandDispatcher(queue, Wrapper("game-A", log)).ProcessNext();
        new ScopedCommandDispatcher(queue, Wrapper("game-B", log)).ProcessNext();

        var scopesSeen = log.Where(entry => !entry.StartsWith("enter:") && !entry.StartsWith("leave:")).ToArray();
        Assert.Equal(new[] { "game-A", "game-B" }, scopesSeen);
    }

    [Fact]
    public void ProcessNext_WrapperFailureIsReported_NotThrown()
    {
        var queue = new ServerQueue("srv", maxCapacity: 5);
        queue.Enqueue(new DelegateCommand(() => { }));

        using var problems = new ProblemSolverThread();
        var signal = new ManualResetEventSlim();
        problems.ProblemLogged += _ => signal.Set();

        var dispatcher = new ScopedCommandDispatcher(
            queue, _ => throw new ArgumentException("нет scope"), problems);

        Assert.True(dispatcher.ProcessNext());
        Assert.True(signal.Wait(2000));
        Assert.Equal(0, dispatcher.Processed);
    }
}