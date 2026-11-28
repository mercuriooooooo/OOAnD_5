namespace SpaceBattle.Lib;

public class ReceiveMessageCommand : ICommand
{
    public IncomingMessage Message { get; }
    public ReceiveMessageCommand(IncomingMessage message) => Message = message;
    public void Execute() { }
}