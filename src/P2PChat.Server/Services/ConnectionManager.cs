using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using P2PChat.Server.Services.Interfaces;
using P2PChat.Server.Collections;

namespace P2PChat.Server.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly BiDictionary<string, string> _connections = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public void AddConnection(string userId, string connectionId)
    {
        _connections.Add(userId, connectionId);
        _logger.LogInformation("Connection added: {UserId} -> {ConnectionId}", userId, connectionId);
    }

    public string? GetConnectionId(string userId) =>
        _connections.TryGetValue(userId, out var connectionId) ? connectionId : null;

    public string? GetUserId(string connectionId) =>
        _connections.TryGetByValue(connectionId, out var userId) ? userId : null;

    public void RemoveConnection(string connectionId)
    {
        if (_connections.RemoveByValue(connectionId))
        {
            _logger.LogInformation("Connection removed: {ConnectionId}", connectionId);
        }
    }
}