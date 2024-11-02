using Microsoft.AspNetCore.Components.Forms;
using P2PChat.Client.Models;
using P2PChat.Client.Store;

namespace P2PChat.Client.ViewModels;

public class ChatViewModel : IDisposable
{
    private readonly IChatStore _store;
    private readonly ILogger<ChatViewModel> _logger;
    private string? _errorMessage;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            NotifyStateChanged();
        }
    }

    public bool IsLoading { get; private set; }
    public string? MyUserId => _store.MyUserId;
    public bool IsConnected => _store.IsConnected;
    public IReadOnlySet<string> ActiveChats => _store.State.ActiveChats;
    public string? SelectedUserId => _store.State.SelectedChatId;
    public bool IsWebRTCConnected => _store.State.ConnectionStates.TryGetValue(SelectedUserId ?? "", out var connected) && connected;

    public event Action? StateChanged;

    public ChatViewModel(IChatStore store, ILogger<ChatViewModel> logger)
    {
        _store = store;
        _logger = logger;
        _store.StateChanged += HandleStoreChanged;
    }

    public async Task Initialize()
    {
        try
        {
            IsLoading = true;
            await _store.WaitForInitialization();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to initialize chat";
            _logger.LogError(ex, "Failed to initialize chat");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public List<ChatMessage> GetCurrentMessages() =>
        _store.State.Messages.TryGetValue(SelectedUserId ?? "", out var messages)
            ? messages
            : new List<ChatMessage>();

    public async Task HandleConnect(string userId)
    {
        try
        {
            IsLoading = true;
            await _store.Connect(userId);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to connect";
            _logger.LogError(ex, "Connection failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task HandleSendMessage(string message)
    {
        try
        {
            await _store.SendMessage(message);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to send message";
            _logger.LogError(ex, "Failed to send message");
        }
    }

    public async Task HandleFileSelected(IBrowserFile file)
    {
        try
        {
            await _store.SendFile(file);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to handle file selection");
        }
    }

    public async Task HandleSelectChat(string userId)
    {
        try
        {
            await _store.SelectChat(userId);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to select chat";
            _logger.LogError(ex, "Failed to select chat");
        }
    }

    private void HandleStoreChanged() => NotifyStateChanged();

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public void Dispose()
    {
        _store.StateChanged -= HandleStoreChanged;
    }

    public void ClearError()
    {
        ErrorMessage = null;
    }
}