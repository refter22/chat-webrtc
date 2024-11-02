using Microsoft.AspNetCore.SignalR;
using P2PChat.Server.Services.Interfaces;
using P2PChat.Shared.Models;

namespace P2PChat.Server.Hubs;

public class SignalingHub : Hub<ISignalingClient>
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

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    [HubMethodName("Register")]
    public async Task Register(string? existingUserId = null)
    {
        try
        {
            _logger.LogInformation("Register called for connection {ConnectionId}", Context.ConnectionId);

            var userId = existingUserId ?? Guid.NewGuid().ToString();
            _connectionManager.AddConnection(userId, Context.ConnectionId);

            _logger.LogInformation("Sending userId {UserId} to client", userId);
            await Clients.Caller.Registered(userId);

            _logger.LogInformation("Register completed for {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Register method");
            throw;
        }
    }

    public async Task RelaySignal(string targetUserId, SignalMessage signal)
    {
        var fromUserId = _connectionManager.GetUserId(Context.ConnectionId);

        if (fromUserId == null)
        {
            _logger.LogWarning("Attempt to relay signal from unregistered connection: {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        signal.FromUserId = fromUserId;
        _logger.LogInformation(
            "Relaying signal {SignalType} from {FromUserId} to {TargetUserId}",
            signal.Type, fromUserId, targetUserId);

        var targetConnectionId = _connectionManager.GetConnectionId(targetUserId);

        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId).ReceiveSignal(signal);
            _logger.LogInformation(
                "Signal {SignalType} successfully relayed: {FromUserId} -> {TargetUserId}",
                signal.Type, fromUserId, targetUserId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to relay signal {SignalType}: target user {TargetUserId} not found",
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