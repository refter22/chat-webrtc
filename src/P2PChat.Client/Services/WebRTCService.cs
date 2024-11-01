using Microsoft.JSInterop;
using P2PChat.Shared.Models;

namespace P2PChat.Client.Services;

public class WebRTCService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly SignalRService _signalRService;
    private readonly WebRTCState _state;
    private readonly ILogger<WebRTCService> _logger;

    public event Action<string>? OnMessageReceived;
    public event Action? OnConnectionEstablished;
    public event Action? OnConnectionClosed;

    public bool IsConnected => _state.IsConnected;
    public string? TargetUserId => _state.TargetUserId;

    public WebRTCService(IJSRuntime jsRuntime, SignalRService signalRService, ILogger<WebRTCService> logger)
    {
        _jsRuntime = jsRuntime;
        _signalRService = signalRService;
        _logger = logger;
        _state = new WebRTCState();

        _signalRService.OnSignalReceived += async (signal) =>
        {
            try
            {
                await HandleSignalReceived(signal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling signal");
            }
        };
    }

    public async Task StartConnection(string targetUserId, bool isInitiator)
    {
        try
        {
            if (_state.IsConnected && _state.TargetUserId == targetUserId)
            {
                return;
            }

            if (_state.IsConnected)
            {
                await DisposeAsync();
            }

            _state.TargetUserId = targetUserId;
            _state.IsInitiator = isInitiator;
            _state.DotNetRef = DotNetObjectReference.Create(this);
            _state.IsConnected = false;
            _state.IsInitialized = false;

            await InitializeWebRTC();

            if (isInitiator)
            {
                await CreateAndSendOffer();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting WebRTC connection");
            throw;
        }
    }

    private async Task InitializeWebRTC()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("webrtc.initialize", _state.DotNetRef, _state.IsInitiator);
            _state.IsInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing WebRTC");
            throw;
        }
    }

    private async Task CreateAndSendOffer()
    {
        try
        {
            var offer = await _jsRuntime.InvokeAsync<object>("webrtc.createOffer");
            await _signalRService.SendSignalAsync(_state.TargetUserId!, new SignalMessage
            {
                Type = SignalType.Offer,
                Data = offer
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/sending offer");
            throw;
        }
    }

    private async Task HandleSignalReceived(SignalMessage signal)
    {
        try
        {
            switch (signal.Type)
            {
                case SignalType.Offer:
                    await HandleOffer(signal);
                    break;
                case SignalType.Answer:
                    await HandleAnswer(signal.Data);
                    break;
                case SignalType.IceCandidate:
                    await HandleIceCandidate(signal.Data);
                    break;
                default:
                    _logger.LogWarning("Unknown signal type: {Type}", signal.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling signal");
            throw;
        }
    }

    private async Task HandleOffer(SignalMessage signal)
    {
        try
        {
            _state.TargetUserId = signal.FromUserId;
            _state.IsInitiator = false;
            _state.DotNetRef = DotNetObjectReference.Create(this);

            await InitializeWebRTC();
            await _jsRuntime.InvokeVoidAsync("webrtc.handleOffer", signal.Data, _state.DotNetRef);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling offer");
            throw;
        }
    }

    private async Task HandleAnswer(object answer)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("webrtc.handleAnswer", answer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling answer");
            throw;
        }
    }

    private async Task HandleIceCandidate(object candidate)
    {
        try
        {
            if (!_state.IsInitialized)
            {
                _logger.LogError("Cannot handle ICE candidate: WebRTC not initialized");
                return;
            }

            await _jsRuntime.InvokeVoidAsync("webrtc.addIceCandidate", candidate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ICE candidate");
            throw;
        }
    }

    [JSInvokable("HandleSignalFromJS")]
    public async Task HandleSignalFromJS(string type, object data)
    {
        try
        {
            if (string.IsNullOrEmpty(_state.TargetUserId))
            {
                _logger.LogError("Cannot send signal: TargetUserId is null");
                return;
            }

            var signal = new SignalMessage
            {
                Type = Enum.Parse<SignalType>(type),
                Data = data,
                FromUserId = _signalRService.UserId
            };

            await _signalRService.SendSignalAsync(_state.TargetUserId, signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling signal from JS");
            throw;
        }
    }

    [JSInvokable("HandleWebRTCMessage")]
    public Task HandleWebRTCMessage(string message)
    {
        try
        {
            OnMessageReceived?.Invoke(message);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebRTC message");
            throw;
        }
    }

    [JSInvokable("HandleWebRTCConnected")]
    public Task HandleWebRTCConnected()
    {
        try
        {
            _state.IsConnected = true;
            OnConnectionEstablished?.Invoke();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebRTC connected");
            throw;
        }
    }

    [JSInvokable("HandleWebRTCClosed")]
    public Task HandleWebRTCClosed()
    {
        try
        {
            _state.IsConnected = false;
            OnConnectionClosed?.Invoke();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebRTC closed");
            throw;
        }
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        try
        {
            if (!_state.IsConnected)
            {
                _logger.LogWarning("Cannot send message: WebRTC not connected");
                return false;
            }

            return await _jsRuntime.InvokeAsync<bool>("webrtc.sendMessage", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message via WebRTC");
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_state.DotNetRef != null)
            {
                _state.DotNetRef.Dispose();
            }

            await _jsRuntime.InvokeVoidAsync("webrtc.dispose");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing WebRTC service");
        }
    }

    [JSInvokable("HandleDataChannelOpen")]
    public Task HandleDataChannelOpen()
    {
        try
        {
            _state.IsConnected = true;
            _state.IsInitialized = true;
            OnConnectionEstablished?.Invoke();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling data channel open");
            throw;
        }
    }

    [JSInvokable("SendSignalFromJS")]
    public async Task SendSignalFromJS(string type, object data)
    {
        try
        {
            if (string.IsNullOrEmpty(_state.TargetUserId))
            {
                return;
            }

            var signal = new SignalMessage
            {
                Type = Enum.Parse<SignalType>(type),
                Data = data,
                FromUserId = _signalRService.UserId
            };

            await _signalRService.SendSignalAsync(_state.TargetUserId, signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending signal from JS");
            throw;
        }
    }
}