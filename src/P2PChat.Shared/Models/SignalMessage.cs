namespace P2PChat.Shared.Models;

public class SignalMessage
{
    public SignalType Type { get; set; }
    public object Data { get; set; } = null!;
    public string? FromUserId { get; set; }
}