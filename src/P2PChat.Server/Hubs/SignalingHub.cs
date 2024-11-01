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
        _logger.LogInformation("Sent Registered event to user: {UserId}", userId);

        await Clients.Others.SendAsync("UserConnected", userId);
        _logger.LogInformation("Sent UserConnected event for user: {UserId}", userId);

        var connectedUsers = _connectionManager.GetAllUsers().ToList();
        _logger.LogInformation("Current connected users: {Users}", string.Join(", ", connectedUsers));

        foreach (var existingUser in connectedUsers.Where(u => u != userId))
        {
            await Clients.Caller.SendAsync("UserConnected", existingUser);
            _logger.LogInformation("Sent existing user {ExistingUser} to new user {NewUser}", existingUser, userId);
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
            await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", signal);
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