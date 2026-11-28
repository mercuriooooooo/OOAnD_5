using System.Collections.Concurrent;

namespace SpaceBattle.Lib;


public class ProblemReport
{
    public Exception Exception { get; init; } = null!;
    public string Context { get; init; } = "";
    public DateTime At { get; init; } = DateTime.UtcNow;
}


public class ProblemSolverThread : IDisposable
{
    private readonly ConcurrentQueue<ProblemReport> _queue = new();
    private readonly Thread _thread;
    private volatile bool _running = true;


    public event Action<ProblemReport>? ProblemLogged;

    public ProblemSolverThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "ProblemSolver" };
        _thread.Start();
    }


    public void Report(Exception exception, string context) =>
        _queue.Enqueue(new ProblemReport { Exception = exception, Context = context });

    private void Run()
    {
        while (_running)
        {
            if (_queue.TryDequeue(out var report))
                ProblemLogged?.Invoke(report);
            else
                Thread.Sleep(2);
        }
    }

    public void Dispose()
    {
        _running = false;
        if (!_thread.Join(2000))
            _thread.Interrupt();
    }
}
