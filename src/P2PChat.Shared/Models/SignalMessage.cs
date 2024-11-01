namespace P2PChat.Shared.Models;

public class SignalMessage
{
    public SignalType Type { get; set; }
    public required object Data { get; set; }
}