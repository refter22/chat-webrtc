using Microsoft.AspNetCore.Components.Forms;
using P2PChat.Client.Models;
using P2PChat.Client.Store.Models;

namespace P2PChat.Client.Store;

public interface IChatStore
{
    ChatState State { get; }
    string? MyUserId { get; }
    bool IsConnected { get; }
    event Action? StateChanged;
    Task WaitForInitialization();
    Task Connect(string userId);
    Task Disconnect(string? userId = null);
    Task SendMessage(string message);
    Task SendFile(IBrowserFile file);
    Task SelectChat(string? userId);
    Task RemoveChat(string userId);
}