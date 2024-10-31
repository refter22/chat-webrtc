using System.Collections.Concurrent;
using P2PChat.Server.Services.Interfaces;

namespace P2PChat.Server.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, string> _connections = new();

    public void AddConnection(string userId, string connectionId)
        => _connections.TryAdd(userId, connectionId);

    public string? GetConnectionId(string userId)
        => _connections.TryGetValue(userId, out var connectionId) ? connectionId : null;

    public void RemoveConnection(string connectionId)
    {
        var kvp = _connections.FirstOrDefault(x => x.Value == connectionId);
        _connections.TryRemove(kvp);
    }
}