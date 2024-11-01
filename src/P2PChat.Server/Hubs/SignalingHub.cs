using Microsoft.AspNetCore.SignalR;
using P2PChat.Server.Services.Interfaces;
using P2PChat.Shared.Models;

namespace P2PChat.Server.Hubs;

public class SignalingHub : Hub
{
    private readonly ILogger<SignalingHub> _logger;
    private readonly IConnectionManager _connectionManager;

    public SignalingHub(
        ILogger<SignalingHub> logger,
        IConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public async Task Register()
    {
        var userId = Guid.NewGuid().ToString();
        _connectionManager.AddConnection(userId, Context.ConnectionId);

        _logger.LogInformation("User registered: {UserId}", userId);
        await Clients.Caller.SendAsync("Registered", userId);
    }

    public async Task RelaySignal(string targetUserId, SignalMessage signal)
    {
        var targetConnectionId = _connectionManager.GetConnectionId(targetUserId);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", signal);
            _logger.LogInformation("Signal {SignalType} relayed to: {TargetUserId}",
                signal.Type, targetUserId);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionManager.RemoveConnection(Context.ConnectionId);
        _logger.LogInformation("User disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}