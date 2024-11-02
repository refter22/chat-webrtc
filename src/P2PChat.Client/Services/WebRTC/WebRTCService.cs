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
        private readonly DotNetObjectReference<WebRTCService> _dotNetRef;
        private readonly Dictionary<string, IJSObjectReference> _connections = new();
        private readonly FileTransferManager _fileTransferManager;

        public event Action<string, string>? OnMessageReceived;
        public event Action<string>? OnConnectionEstablished;
        public event Action<string>? OnConnectionClosed;
        public event Action<string, FileMetadata>? OnFileReceiveStarted;
        public event Action<string, FileChunk>? OnFileChunkReceived;
        public event Action<string>? OnFileReceiveCompleted;

        public WebRTCService(
            IJSRuntime jsRuntime,
            SignalRService signalRService,
            ILogger<WebRTCService> logger,
            FileTransferManager fileTransferManager)
        {
            _jsRuntime = jsRuntime;
            _signalRService = signalRService;
            _logger = logger;
            _dotNetRef = DotNetObjectReference.Create(this);
            _fileTransferManager = fileTransferManager;

            _signalRService.OnSignalReceived += HandleSignalReceived;
        }

        public async Task StartConnection(string targetUserId, bool isInitiator)
        {
            try
            {
                if (_connections.ContainsKey(targetUserId))
                {
                    await CloseConnection(targetUserId);
                }

                var connection = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "webrtc.createConnection",
                    _dotNetRef,
                    targetUserId
                );

                _connections[targetUserId] = connection;

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
            if (!_connections.TryGetValue(targetUserId, out var connection))
            {
                throw new InvalidOperationException($"No connection found for {targetUserId}");
            }

            var offer = await _jsRuntime.InvokeAsync<object>("webrtc.createOffer", connection);
            await _signalRService.SendSignalAsync(targetUserId, new SignalMessage
            {
                Type = SignalType.Offer,
                Data = offer,
                FromUserId = _signalRService.UserId
            });
        }

        private async Task HandleSignalReceived(SignalMessage signal)
        {
            if (signal.FromUserId == null) return;

            try
            {
                switch (signal.Type)
                {
                    case SignalType.Offer:
                        await HandleOffer(signal);
                        break;
                    case SignalType.Answer:
                        if (_connections.TryGetValue(signal.FromUserId, out var conn))
                        {
                            await _jsRuntime.InvokeVoidAsync("webrtc.handleAnswer", conn, signal.Data);
                        }
                        break;
                    case SignalType.IceCandidate:
                        if (_connections.TryGetValue(signal.FromUserId, out conn))
                        {
                            await _jsRuntime.InvokeVoidAsync("webrtc.addIceCandidate", conn, signal.Data);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling signal of type {Type}", signal.Type);
            }
        }

        private async Task HandleOffer(SignalMessage signal)
        {
            if (signal.FromUserId == null) return;

            await StartConnection(signal.FromUserId, false);

            if (_connections.TryGetValue(signal.FromUserId, out var connection))
            {
                var answer = await _jsRuntime.InvokeAsync<object>("webrtc.handleOffer", connection, signal.Data);
                await _signalRService.SendSignalAsync(signal.FromUserId, new SignalMessage
                {
                    Type = SignalType.Answer,
                    Data = answer,
                    FromUserId = _signalRService.UserId
                });
            }
        }

        public async Task SendMessageAsync(string targetUserId, string message)
        {
            try
            {
                if (!_connections.TryGetValue(targetUserId, out var connection))
                {
                    _logger.LogError($"No connection found for {targetUserId}");
                    throw new InvalidOperationException($"No connection found for {targetUserId}");
                }

                var data = JsonSerializer.Serialize(new { type = "message", content = message });
                _logger.LogInformation($"Sending data to {targetUserId}: {data}");

                var success = await _jsRuntime.InvokeAsync<bool>("webrtc.sendData", connection, data);

                if (!success)
                {
                    _logger.LogError($"Failed to send message to {targetUserId}");
                    throw new InvalidOperationException("Failed to send message");
                }

                _logger.LogInformation($"Message sent successfully to {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message to {targetUserId}");
                throw;
            }
        }

        public async Task SendFileAsync(string targetUserId, IBrowserFile file)
        {
            if (!_connections.TryGetValue(targetUserId, out var connection))
            {
                throw new InvalidOperationException($"No connection found for {targetUserId}");
            }

            await _fileTransferManager.SendFile(targetUserId, file, connection);
        }

        [JSInvokable]
        public Task HandleConnectionOpened(string targetUserId)
        {
            _logger.LogInformation("Connection opened with {UserId}", targetUserId);
            OnConnectionEstablished?.Invoke(targetUserId);
            return Task.CompletedTask;
        }

        [JSInvokable]
        public Task HandleConnectionClosed(string targetUserId)
        {
            _logger.LogInformation("Connection closed with {UserId}", targetUserId);
            OnConnectionClosed?.Invoke(targetUserId);
            return Task.CompletedTask;
        }

        [JSInvokable]
        public Task HandleDataReceived(string targetUserId, string data)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(data);
                var messageType = message.GetProperty("type").GetString();
                _logger.LogInformation($"Received data type: {messageType} from {targetUserId}");

                switch (messageType)
                {
                    case "message":
                        var content = message.GetProperty("content").GetString()!;
                        OnMessageReceived?.Invoke(targetUserId, content);
                        break;

                    case "file-start":
                        var metadata = JsonSerializer.Deserialize<FileMetadata>(
                            message.GetProperty("metadata").GetRawText())!;
                        _fileTransferManager.HandleFileStart(targetUserId, metadata);
                        break;

                    case "file-chunk":
                        var chunk = JsonSerializer.Deserialize<FileChunk>(
                            message.GetProperty("chunk").GetRawText())!;
                        _fileTransferManager.HandleFileChunk(targetUserId, chunk);
                        break;

                    case "file-end":
                        _fileTransferManager.HandleFileEnd(targetUserId);
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

        public async Task CloseConnection(string targetUserId)
        {
            if (_connections.TryGetValue(targetUserId, out var connection))
            {
                await _jsRuntime.InvokeVoidAsync("webrtc.closeConnection", connection);
                await connection.DisposeAsync();
                _connections.Remove(targetUserId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var targetUserId in _connections.Keys.ToList())
            {
                await CloseConnection(targetUserId);
            }
            _signalRService.OnSignalReceived -= HandleSignalReceived;
            _dotNetRef.Dispose();
        }
    }
}