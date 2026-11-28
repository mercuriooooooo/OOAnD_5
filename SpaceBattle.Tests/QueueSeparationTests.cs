using SpaceBattle.Lib;
using Xunit;

public class QueueSeparationTests
{
    [Fact]
    public void ServerQueue_ImplementsISPInterfaces()
    {
        ServerQueue queue = new("test-server");
        Assert.IsAssignableFrom<IQueueWriter>(queue);
        Assert.IsAssignableFrom<IQueueReader>(queue);
    }

    [Fact]
    public void Backpressure_QueueFull_RejectsEnqueue()
    {
        ServerQueue queue = new("test", maxCapacity: 3);
        var cmd = new ReceiveMessageCommand(new IncomingMessage());

        IQueueWriter writer = queue;
        Assert.True(writer.TryEnqueue(cmd));
        Assert.True(writer.TryEnqueue(cmd));
        Assert.True(writer.TryEnqueue(cmd));
        var rejected = writer.TryEnqueue(cmd);
        Assert.False(rejected);
    }

    [Fact]
    public void Reader_TryDequeue_GetsItemsInOrder()
    {
        ServerQueue queue = new("test", maxCapacity: 5);
        var msg1 = new IncomingMessage { GameId = "g1" };
        var msg2 = new IncomingMessage { GameId = "g2" };
        queue.Enqueue(new ReceiveMessageCommand(msg1));
        queue.Enqueue(new ReceiveMessageCommand(msg2));

        IQueueReader reader = queue;
        Assert.Equal(2, reader.Count);
        Assert.True(reader.TryDequeue(out var first));
        Assert.Equal("g1", ((ReceiveMessageCommand)first).Message.GameId);
        Assert.True(reader.TryDequeue(out var second));
        Assert.Equal("g2", ((ReceiveMessageCommand)second).Message.GameId);
        Assert.Equal(0, reader.Count);
    }
}
