using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using P2PChat.Client.Models;
using P2PChat.Client.Services;
using P2PChat.Client.Services.WebRTC;

namespace P2PChat.Client.ViewModels;

public class ChatViewModel : IDisposable
{
    private readonly WebRTCService _webRTCService;
    private readonly SignalRService _signalRService;
    private readonly ILogger<ChatViewModel> _logger;
    private readonly FileTransferManager _fileTransferManager;
    private readonly IJSRuntime _jsRuntime;
    private string? _errorMessage;
    private HashSet<string> _activeChats = new();
    public IReadOnlySet<string> ActiveChats => _activeChats;
    public Dictionary<string, List<ChatMessage>> ChatMessages { get; } = new();
    public string? SelectedUserId { get; private set; }
    public bool IsWebRTCConnected { get; private set; }
    public string NewMessage { get; set; } = "";
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            NotifyStateChanged();
        }
    }
    public string? MyUserId => _signalRService.UserId;
    public bool IsConnected => _signalRService.IsConnected;
    public event Action? StateChanged;

    public ChatViewModel(
        WebRTCService webRTCService,
        SignalRService signalRService,
        ILogger<ChatViewModel> logger,
        FileTransferManager fileTransferManager,
        IJSRuntime jsRuntime)
    {
        _webRTCService = webRTCService;
        _signalRService = signalRService;
        _logger = logger;
        _fileTransferManager = fileTransferManager;
        _jsRuntime = jsRuntime;

        _webRTCService.OnMessageReceived += HandleMessageReceived;
        _webRTCService.OnConnectionEstablished += HandleWebRTCConnected;
        _webRTCService.OnConnectionClosed += HandleWebRTCClosed;
        _signalRService.UserIdChanged += HandleUserIdChanged;
        _fileTransferManager.OnFileMessageCreated += HandleFileMessageCreated;
        _fileTransferManager.StateChanged += HandleFileTransferStateChanged;
    }

    public async Task Initialize()
    {
        try
        {
            var savedChats = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "activeChats");
            if (!string.IsNullOrEmpty(savedChats))
            {
                _activeChats = new HashSet<string>(savedChats.Split(','));
                _logger.LogInformation($"Loaded {_activeChats.Count} saved chats");
            }

            await _signalRService.StartAsync();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chat");
            ErrorMessage = "Failed to initialize chat";
        }
    }

    public List<ChatMessage> GetCurrentMessages()
    {
        if (string.IsNullOrEmpty(SelectedUserId))
            return new List<ChatMessage>();

        if (!ChatMessages.ContainsKey(SelectedUserId))
            ChatMessages[SelectedUserId] = new List<ChatMessage>();

        return ChatMessages[SelectedUserId];
    }

    public async Task Connect(string targetUserId)
    {
        if (string.IsNullOrWhiteSpace(targetUserId)) return;

        if (targetUserId == MyUserId)
        {
            _logger.LogWarning("Cannot connect to self");
            ErrorMessage = "Cannot connect to yourself";
            return;
        }

        try
        {
            SelectedUserId = targetUserId;
            _activeChats.Add(targetUserId);
            await SaveActiveChats();

            IsWebRTCConnected = false;
            await _webRTCService.StartConnection(targetUserId, true);
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed");
            ErrorMessage = "Connection failed";
            SelectedUserId = null;
            _activeChats.Remove(targetUserId);
            await SaveActiveChats();
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public void Dispose()
    {
        _webRTCService.OnMessageReceived -= HandleMessageReceived;
        _webRTCService.OnConnectionEstablished -= HandleWebRTCConnected;
        _webRTCService.OnConnectionClosed -= HandleWebRTCClosed;
        _signalRService.UserIdChanged -= HandleUserIdChanged;

        _fileTransferManager.OnFileMessageCreated -= HandleFileMessageCreated;
        _fileTransferManager.StateChanged -= HandleFileTransferStateChanged;
    }

    public async Task SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || SelectedUserId == null) return;

        try
        {
            _logger.LogInformation($"Sending message to {SelectedUserId}: {message}");
            await _webRTCService.SendMessageAsync(SelectedUserId, message);

            if (!ChatMessages.ContainsKey(SelectedUserId))
            {
                ChatMessages[SelectedUserId] = new List<ChatMessage>();
            }

            ChatMessages[SelectedUserId].Add(new ChatMessage
            {
                Text = message,
                IsFromMe = true,
                IsFile = false,
                Timestamp = DateTime.Now
            });

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            ErrorMessage = "Failed to send message";
            NotifyStateChanged();
        }
    }

    public async Task HandleFileSelected(IBrowserFile file)
    {
        try
        {
            if (SelectedUserId == null)
            {
                ErrorMessage = "Please select a chat before sending a file";
                NotifyStateChanged();
                return;
            }

            if (file.Size > 100 * 1024 * 1024) // 100MB
            {
                ErrorMessage = "File size exceeds maximum allowed size of 100MB";
                NotifyStateChanged();
                return;
            }

            await _webRTCService.SendFileAsync(SelectedUserId, file);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to handle file selection");
            NotifyStateChanged();
        }
    }

    private void HandleMessageReceived(string userId, string message)
    {
        if (!ChatMessages.ContainsKey(userId))
        {
            ChatMessages[userId] = new List<ChatMessage>();
        }

        ChatMessages[userId].Add(new ChatMessage
        {
            Text = message,
            IsFromMe = false,
            Timestamp = DateTime.Now
        });

        NotifyStateChanged();
    }

    private void HandleWebRTCConnected(string userId)
    {
        IsWebRTCConnected = true;
        NotifyStateChanged();
    }

    private void HandleWebRTCClosed(string userId)
    {
        IsWebRTCConnected = false;
        NotifyStateChanged();
    }

    private void HandleUserIdChanged(string? userId)
    {
        NotifyStateChanged();
    }

    private async Task SaveActiveChats()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "activeChats", string.Join(",", _activeChats));
    }

    public async Task SwitchChat(string chatId)
    {
        if (SelectedUserId == chatId) return;

        await Disconnect();
        SelectedUserId = chatId;
        await Connect(chatId);
    }

    public async Task RemoveChat(string chatId)
    {
        _activeChats.Remove(chatId);
        await SaveActiveChats();

        if (SelectedUserId == chatId)
        {
            await Disconnect();
        }
    }

    public async Task Disconnect()
    {
        if (SelectedUserId != null)
        {
            await _webRTCService.CloseConnection(SelectedUserId);
            SelectedUserId = null;
            IsWebRTCConnected = false;
            NotifyStateChanged();
        }
    }

    private void HandleFileMessageCreated(string userId, ChatMessage message)
    {
        if (!ChatMessages.ContainsKey(userId))
        {
            ChatMessages[userId] = new List<ChatMessage>();
        }

        ChatMessages[userId].Add(message);
        NotifyStateChanged();
    }

    private void HandleFileTransferStateChanged()
    {
        NotifyStateChanged();
    }
}