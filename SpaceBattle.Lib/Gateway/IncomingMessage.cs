namespace SpaceBattle.Lib;

public class IncomingMessage
{
    public string GameId { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Type { get; set; } = "";
    public string PayloadJson { get; set; } = "";
}