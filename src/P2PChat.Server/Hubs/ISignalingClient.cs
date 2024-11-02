using P2PChat.Shared.Models;

namespace P2PChat.Server.Hubs;

public interface ISignalingClient
{
    Task Registered(string userId);
    Task ReceiveSignal(SignalMessage signal);
}