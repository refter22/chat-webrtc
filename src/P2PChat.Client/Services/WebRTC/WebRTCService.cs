using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using P2PChat.Client.Models;
using P2PChat.Shared.Models;

namespace P2PChat.Client.Services.WebRTC
{
    public class WebRTCService : IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly SignalRService _signalRService;
        private readonly ILogger<WebRTCService> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly FileTransferManager _fileManager;
        private readonly DotNetObjectReference<WebRTCService> _dotNetRef;

        public event Action<string, string>? OnMessageReceived;
        public event Action<string>? OnConnectionEstablished;
        public event Action<string>? OnConnectionClosed;
        public event Action<string, FileMetadata>? OnFileReceiveStarted;
        public event Action<string, FileChunk>? OnFileChunkReceived;
        public event Action<string>? OnFileReceiveCompleted;

        public bool IsConnected => _connectionManager.HasActiveConnections;
        public string? TargetUserId => _connectionManager.GetActiveConnectionId();

        public WebRTCService(
            IJSRuntime jsRuntime,
            SignalRService signalRService,
            ILogger<WebRTCService> logger)
        {
            _jsRuntime = jsRuntime;
            _signalRService = signalRService;
            _logger = logger;
            _dotNetRef = DotNetObjectReference.Create(this);
            _connectionManager = new ConnectionManager(_jsRuntime, logger);
            _fileManager = new FileTransferManager(_jsRuntime, logger);

            _signalRService.OnSignalReceived += HandleSignalReceived;
        }

        public async Task StartConnection(string targetUserId, bool isInitiator)
        {
            try
            {
                await _connectionManager.StartConnection(targetUserId, isInitiator, _dotNetRef);
                if (isInitiator)
                {
                    await CreateAndSendOffer(targetUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start connection with {UserId}", targetUserId);
                throw;
            }
        }

        private async Task CreateAndSendOffer(string targetUserId)
        {
            var offer = await _jsRuntime.InvokeAsync<object>("webrtc.createOffer", targetUserId);
            await _signalRService.SendSignalAsync(targetUserId, new SignalMessage
            {
                Type = SignalType.Offer,
                Data = offer,
                FromUserId = _signalRService.UserId
            });
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
                        await _jsRuntime.InvokeVoidAsync("webrtc.handleAnswer", signal.FromUserId, signal.Data);
                        break;
                    case SignalType.IceCandidate:
                        await _jsRuntime.InvokeVoidAsync("webrtc.addIceCandidate", signal.FromUserId, signal.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling signal of type {Type}", signal.Type);
                throw;
            }
        }

        private async Task HandleOffer(SignalMessage signal)
        {
            await StartConnection(signal.FromUserId!, false);
            var answer = await _jsRuntime.InvokeAsync<object>("webrtc.handleOffer", signal.FromUserId, signal.Data);
            await _signalRService.SendSignalAsync(signal.FromUserId!, new SignalMessage
            {
                Type = SignalType.Answer,
                Data = answer,
                FromUserId = _signalRService.UserId
            });
        }

        public async Task SendMessageAsync(string targetUserId, string message)
        {
            if (!_connectionManager.IsConnected(targetUserId))
            {
                throw new InvalidOperationException($"No active WebRTC connection with {targetUserId}");
            }

            var messageData = new { type = "message", content = message };
            var success = await _jsRuntime.InvokeAsync<bool>(
                "webrtc.sendData",
                targetUserId,
                JsonSerializer.Serialize(messageData));

            if (!success)
            {
                throw new InvalidOperationException("Failed to send message");
            }
        }

        public async Task SendFileAsync(string targetUserId, IBrowserFile file)
        {
            if (!_connectionManager.IsConnected(targetUserId))
            {
                throw new InvalidOperationException($"No active WebRTC connection with {targetUserId}");
            }

            await _fileManager.SendFile(targetUserId, file);
        }

        [JSInvokable]
        public Task HandleConnectionOpened(string targetUserId)
        {
            _connectionManager.SetConnectionState(targetUserId, true);
            OnConnectionEstablished?.Invoke(targetUserId);
            return Task.CompletedTask;
        }

        [JSInvokable]
        public Task HandleConnectionClosed(string targetUserId)
        {
            _connectionManager.SetConnectionState(targetUserId, false);
            OnConnectionClosed?.Invoke(targetUserId);
            return Task.CompletedTask;
        }

        [JSInvokable]
        public Task HandleDataReceived(string targetUserId, string data)
        {
            try
            {
                _logger.LogInformation("Received data from {UserId}: {Data}", targetUserId, data);
                var message = JsonSerializer.Deserialize<JsonElement>(data);

                switch (message.GetProperty("type").GetString())
                {
                    case "message":
                        var content = message.GetProperty("content").GetString()!;
                        _logger.LogInformation("Received message: {Content}", content);
                        OnMessageReceived?.Invoke(targetUserId, content);
                        break;

                    case "file-start":
                        var metadata = JsonSerializer.Deserialize<FileMetadata>(message.GetProperty("metadata").GetRawText())!;
                        _fileManager.HandleFileStart(targetUserId, metadata);
                        OnFileReceiveStarted?.Invoke(targetUserId, metadata);
                        break;

                    case "file-chunk":
                        var chunk = JsonSerializer.Deserialize<FileChunk>(message.GetProperty("chunk").GetRawText())!;
                        _fileManager.HandleFileChunk(targetUserId, chunk);
                        OnFileChunkReceived?.Invoke(targetUserId, chunk);
                        break;

                    case "file-end":
                        _fileManager.HandleFileEnd(targetUserId);
                        OnFileReceiveCompleted?.Invoke(targetUserId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received data");
            }

            return Task.CompletedTask;
        }

        [JSInvokable]
        public async Task HandleIceCandidate(string targetUserId, object candidate)
        {
            await _signalRService.SendSignalAsync(targetUserId, new SignalMessage
            {
                Type = SignalType.IceCandidate,
                Data = candidate,
                FromUserId = _signalRService.UserId
            });
        }

        public async ValueTask DisposeAsync()
        {
            await _connectionManager.DisposeAllConnections();
            _signalRService.OnSignalReceived -= HandleSignalReceived;
            _dotNetRef.Dispose();
        }
    }
}