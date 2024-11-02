using P2PChat.Client.Models;

namespace P2PChat.Client.Store.Models;

public record ChatState
{
    public HashSet<string> ActiveChats { get; init; } = new();
    public Dictionary<string, List<ChatMessage>> Messages { get; init; } = new();
    public string? SelectedChatId { get; init; }
    public Dictionary<string, bool> ConnectionStates { get; init; } = new();
}