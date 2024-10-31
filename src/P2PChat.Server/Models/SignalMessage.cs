namespace P2PChat.Server.Models;

public class SignalMessage
{
    public SignalType Type { get; set; }
    public required object Data { get; set; }
}