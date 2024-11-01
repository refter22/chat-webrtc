using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using P2PChat.Server.Services.Interfaces;

namespace P2PChat.Server.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly Dictionary<string, string> _userConnections = new();
    private readonly Dictionary<string, string> _connectionUsers = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public void AddConnection(string userId, string connectionId)
    {
        _userConnections[userId] = connectionId;
        _connectionUsers[connectionId] = userId;
        _logger.LogInformation("Added connection mapping: User {UserId} -> Connection {ConnectionId}",
            userId, connectionId);
    }

    public string? GetConnectionId(string userId)
    {
        if (_userConnections.TryGetValue(userId, out var connectionId))
        {
            return connectionId;
        }
        _logger.LogWarning("Connection ID not found for user {UserId}", userId);
        return null;
    }

    public string? GetUserId(string connectionId)
    {
        if (_connectionUsers.TryGetValue(connectionId, out var userId))
        {
            return userId;
        }
        _logger.LogWarning("User ID not found for connection {ConnectionId}", connectionId);
        return null;
    }

    public void RemoveConnection(string connectionId)
    {
        if (_connectionUsers.TryGetValue(connectionId, out var userId))
        {
            _userConnections.Remove(userId);
            _connectionUsers.Remove(connectionId);
            _logger.LogInformation("Removed connection mapping for User {UserId}", userId);
        }
    }

    public IEnumerable<string> GetAllUsers()
    {
        return _userConnections.Keys.ToList();
    }
}