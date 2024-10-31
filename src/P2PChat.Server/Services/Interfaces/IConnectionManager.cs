namespace P2PChat.Server.Services.Interfaces;

public interface IConnectionManager
{
    void AddConnection(string userId, string connectionId);
    string? GetConnectionId(string userId);
    void RemoveConnection(string connectionId);
}