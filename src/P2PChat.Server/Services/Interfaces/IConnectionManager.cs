namespace P2PChat.Server.Services.Interfaces;

public interface IConnectionManager
{
    void AddConnection(string userId, string connectionId);
    string? GetConnectionId(string userId);
    string? GetUserId(string connectionId);
    void RemoveConnection(string connectionId);
}