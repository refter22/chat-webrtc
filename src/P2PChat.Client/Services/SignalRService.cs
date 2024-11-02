using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http.Connections;
using P2PChat.Shared.Models;

namespace P2PChat.Client.Services;

public class SignalRService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SignalRService> _logger;
    private string? _userId;
    public event Action? UserIdChanged;
    public SignalMessage? CurrentSignal { get; private set; }
    public event Action<string>? OnConnected;
    public event Action<SignalMessage>? OnSignalReceived;
    public event Action<string>? OnUserConnected;

    public SignalRService(IConfiguration configuration, NavigationManager navigationManager, ILogger<SignalRService> logger)
    {
        _configuration = configuration;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_hubConnection != null) return;

        var hubUrl = Environment.GetEnvironmentVariable("SIGNALR_HUB_URL")
            ?? _configuration["SIGNALR_HUB_URL"]
            ?? "http://localhost:5056/signaling";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.SkipNegotiation = true;
                options.Transports = HttpTransportType.WebSockets;
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string>("Registered", HandleRegistered);

        _hubConnection.On<string>("UserConnected", userId =>
        {
            OnUserConnected?.Invoke(userId);
        });

        _hubConnection.On<SignalMessage>("ReceiveSignal", HandleSignalReceived);

        try
        {
            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("Register", (string?)null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            throw;
        }
    }

    public async Task SendSignalAsync(string targetUserId, SignalMessage signal)
    {
        try
        {
            if (_hubConnection == null)
            {
                throw new InvalidOperationException("Hub connection is not initialized");
            }

            await _hubConnection.InvokeAsync("RelaySignal", targetUserId, signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signal");
            throw;
        }
    }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    private void HandleSignalReceived(SignalMessage signal)
    {
        try
        {
            CurrentSignal = signal;
            OnSignalReceived?.Invoke(signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling signal");
        }
    }

    private void HandleRegistered(string userId)
    {
        UserId = userId;
        OnConnected?.Invoke(userId);
    }

    public string? UserId
    {
        get => _userId;
        private set
        {
            if (_userId != value)
            {
                _userId = value;
                UserIdChanged?.Invoke();
            }
        }
    }
}