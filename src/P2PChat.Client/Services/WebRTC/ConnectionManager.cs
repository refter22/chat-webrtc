using Microsoft.JSInterop;
using P2PChat.Client.Models;

namespace P2PChat.Client.Services.WebRTC;

internal class ConnectionManager
{
    private readonly Dictionary<string, bool> _connectionStates = new();
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;

    public bool HasActiveConnections => _connectionStates.Values.Any(state => state);

    public string? GetActiveConnectionId() =>
        _connectionStates.FirstOrDefault(x => x.Value).Key;

    public ConnectionManager(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task StartConnection(string targetUserId, bool isInitiator, DotNetObjectReference<WebRTCService> dotNetRef)
    {
        try
        {
            if (_connectionStates.ContainsKey(targetUserId))
            {
                if (_connectionStates[targetUserId])
                {
                    return;
                }
                await DisposeConnection(targetUserId);
            }

            var success = await _jsRuntime.InvokeAsync<bool>(
                "webrtc.initialize",
                dotNetRef,
                isInitiator,
                targetUserId);

            if (!success)
            {
                throw new Exception("Failed to initialize WebRTC connection");
            }

            _connectionStates[targetUserId] = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start connection with {UserId}", targetUserId);
            throw;
        }
    }

    public async Task DisposeConnection(string targetUserId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("webrtc.dispose", targetUserId);
            _connectionStates.Remove(targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing connection for {UserId}", targetUserId);
            throw;
        }
    }

    public async Task DisposeAllConnections()
    {
        foreach (var targetUserId in _connectionStates.Keys.ToList())
        {
            await DisposeConnection(targetUserId);
        }
    }

    public bool IsConnected(string targetUserId)
    {
        return _connectionStates.TryGetValue(targetUserId, out var isConnected) && isConnected;
    }

    public void SetConnectionState(string targetUserId, bool isConnected)
    {
        _connectionStates[targetUserId] = isConnected;
    }
}