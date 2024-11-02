using Microsoft.AspNetCore.Components.Forms;
using P2PChat.Client.Models;
using P2PChat.Client.Services;
using P2PChat.Client.Services.Storage;
using P2PChat.Client.Services.WebRTC;
using P2PChat.Client.Store.Models;
using P2PChat.Shared.Models;

namespace P2PChat.Client.Store;

public class ChatStore : IChatStore, IAsyncDisposable
{
    private readonly IStorageService _storage;
    private readonly WebRTCService _webRTCService;
    private readonly SignalRService _signalRService;
    private readonly FileTransferManager _fileTransferManager;
    private readonly ILogger<ChatStore> _logger;
    private const string STORAGE_KEY = "chat_state";
    private ChatState _state = new();
    private readonly Task _initializationTask;

    public ChatState State => _state;
    public string? MyUserId => _signalRService.UserId;
    public bool IsConnected => _signalRService.IsConnected;
    public event Action? StateChanged;

    public ChatStore(
        IStorageService storage,
        WebRTCService webRTCService,
        SignalRService signalRService,
        FileTransferManager fileTransferManager,
        ILogger<ChatStore> logger)
    {
        _storage = storage;
        _webRTCService = webRTCService;
        _signalRService = signalRService;
        _fileTransferManager = fileTransferManager;
        _logger = logger;

        _webRTCService.OnConnectionEstablished += HandleWebRTCConnected;
        _webRTCService.OnConnectionClosed += HandleWebRTCClosed;
        _webRTCService.OnMessageReceived += HandleMessageReceived;
        _signalRService.UserIdChanged += HandleUserIdChanged;
        _fileTransferManager.OnFileMessageCreated += HandleFileMessageCreated;
        _fileTransferManager.StateChanged += HandleFileTransferStateChanged;

        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var savedState = await _storage.LoadAsync<ChatState>(STORAGE_KEY);
            if (savedState != null)
            {
                _state = savedState;
                _logger.LogInformation("Chat state loaded from storage");

                foreach (var userId in _state.ActiveChats)
                {
                    if (_state.ConnectionStates.TryGetValue(userId, out var isConnected) && isConnected)
                    {
                        try
                        {
                            await _webRTCService.StartConnection(userId, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to restore connection with {UserId}", userId);
                        }
                    }
                }
            }

            await _signalRService.StartAsync();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chat store");
            throw;
        }
    }

    public Task WaitForInitialization() => _initializationTask;

    public async Task Connect(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        if (userId == MyUserId)
            throw new InvalidOperationException("Cannot connect to yourself");

        try
        {
            await _webRTCService.StartConnection(userId, true);

            _state = _state with
            {
                ConnectionStates = new(_state.ConnectionStates)
                {
                    [userId] = false
                }
            };

            await SaveState();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to user {UserId}", userId);
            throw;
        }
    }

    public async Task SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        if (string.IsNullOrEmpty(_state.SelectedChatId))
            throw new InvalidOperationException("No chat selected");

        try
        {
            await _webRTCService.SendMessageAsync(_state.SelectedChatId, message);

            var chatMessage = new ChatMessage
            {
                Text = message,
                IsFromMe = true,
                IsFile = false,
                Timestamp = DateTime.Now
            };

            await AddMessage(_state.SelectedChatId, chatMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            throw;
        }
    }

    public async Task SendFile(IBrowserFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        if (string.IsNullOrEmpty(_state.SelectedChatId))
            throw new InvalidOperationException("No chat selected");

        if (file.Size > 100 * 1024 * 1024) // 100MB
            throw new InvalidOperationException("File size exceeds maximum allowed size of 100MB");

        try
        {
            await _webRTCService.SendFileAsync(_state.SelectedChatId, file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send file");
            throw;
        }
    }

    public async Task Disconnect(string? userId = null)
    {
        userId ??= _state.SelectedChatId;

        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _webRTCService.CloseConnection(userId);

            _state = _state with
            {
                ConnectionStates = new(_state.ConnectionStates)
                {
                    [userId] = false
                },
                SelectedChatId = _state.SelectedChatId == userId ? null : _state.SelectedChatId
            };

            await SaveState();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect from user {UserId}", userId);
            throw;
        }
    }

    private async Task AddMessage(string userId, ChatMessage message)
    {
        var messages = _state.Messages.TryGetValue(userId, out var existing)
            ? new List<ChatMessage>(existing)
            : new List<ChatMessage>();

        messages.Add(message);

        _state = _state with
        {
            Messages = new(_state.Messages)
            {
                [userId] = messages
            }
        };

        await SaveState();
        NotifyStateChanged();
    }

    private async Task AddChat(string userId)
    {
        if (_state.ActiveChats.Contains(userId)) return;

        _state = _state with
        {
            ActiveChats = new(_state.ActiveChats) { userId }
        };

        await SaveState();
        NotifyStateChanged();
    }

    public async Task RemoveChat(string userId)
    {
        if (!_state.ActiveChats.Contains(userId)) return;

        await Disconnect(userId);

        _state = _state with
        {
            ActiveChats = new(_state.ActiveChats.Where(x => x != userId)),
            Messages = new(_state.Messages.Where(x => x.Key != userId)
                .ToDictionary(x => x.Key, x => x.Value))
        };

        await SaveState();
        NotifyStateChanged();
    }

    public async Task SelectChat(string? userId)
    {
        if (_state.SelectedChatId == userId) return;

        _state = _state with { SelectedChatId = userId };
        await SaveState();
        NotifyStateChanged();
    }

    private void HandleMessageReceived(string userId, string message)
    {
        var chatMessage = new ChatMessage
        {
            Text = message,
            IsFromMe = false,
            Timestamp = DateTime.Now
        };

        _ = AddMessage(userId, chatMessage);
    }

    private void HandleWebRTCConnected(string userId)
    {
        try
        {
            _logger.LogInformation("WebRTC connection established with {UserId}", userId);

            _ = AddChat(userId);

            _state = _state with
            {
                ConnectionStates = new(_state.ConnectionStates)
                {
                    [userId] = true
                }
            };

            _ = SaveState();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebRTC connection from {UserId}", userId);
        }
    }

    private void HandleWebRTCClosed(string userId)
    {
        _state = _state with
        {
            ConnectionStates = new(_state.ConnectionStates)
            {
                [userId] = false
            }
        };

        _ = SaveState();
        NotifyStateChanged();
    }

    private void HandleUserIdChanged(string? userId)
    {
        NotifyStateChanged();
    }

    private void HandleFileMessageCreated(string userId, ChatMessage message)
    {
        _ = AddMessage(userId, message);
    }

    private void HandleFileTransferStateChanged()
    {
        NotifyStateChanged();
    }

    private async Task SaveState()
    {
        try
        {
            await _storage.SaveAsync(STORAGE_KEY, _state);
            _logger.LogInformation("Chat state saved to storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save chat state");
            throw;
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _initializationTask;
        }
        catch { }

        _webRTCService.OnMessageReceived -= HandleMessageReceived;
        _webRTCService.OnConnectionEstablished -= HandleWebRTCConnected;
        _webRTCService.OnConnectionClosed -= HandleWebRTCClosed;
        _signalRService.UserIdChanged -= HandleUserIdChanged;
        _fileTransferManager.OnFileMessageCreated -= HandleFileMessageCreated;
        _fileTransferManager.StateChanged -= HandleFileTransferStateChanged;

        await Disconnect();
    }
}