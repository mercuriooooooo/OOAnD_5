namespace SpaceBattle.Lib;

public class ScopedCommandDispatcher
{
    private readonly IQueueReader _reader;
    private readonly Func<ICommand, ICommand> _wrapForScope;
    private readonly ProblemSolverThread? _problems;
    private long _processed;

    public ScopedCommandDispatcher(
        IQueueReader reader,
        Func<ICommand, ICommand> wrapForScope,
        ProblemSolverThread? problems = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(wrapForScope);

        _reader = reader;
        _wrapForScope = wrapForScope;
        _problems = problems;
    }

    public long Processed => Interlocked.Read(ref _processed);

    public bool ProcessNext()
    {
        if (!_reader.TryDequeue(out var command))
            return false;

        try
        {
            _wrapForScope(command).Execute();
            Interlocked.Increment(ref _processed);
        }
        catch (Exception exception)
        {
            if (_problems is null)
                throw;
            _problems.Report(exception, $"Единица работы игры не выполнена: {exception.Message}");
        }

        return true;
    }

    public int ProcessAvailable()
    {
        var count = 0;
        while (ProcessNext())
            count++;
        return count;
    }
}