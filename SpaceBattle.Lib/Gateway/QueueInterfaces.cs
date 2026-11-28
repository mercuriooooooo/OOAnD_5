namespace SpaceBattle.Lib;

public interface IQueueWriter
{
    bool TryEnqueue(ICommand command);
}

public interface IQueueReader
{
    bool TryDequeue(out ICommand command);
    int Count { get; }
}

public interface IServerQueue : IQueueWriter, IQueueReader
{
    string ServerId { get; }
    void Enqueue(ICommand command);
    int Count { get; }
}