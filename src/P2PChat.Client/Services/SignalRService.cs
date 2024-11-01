using Microsoft.AspNetCore.SignalR.Client;
using P2PChat.Shared.Models;

namespace P2PChat.Client.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;
    private string? _userId;

    public event Action<string>? OnRegistered;
    public event Action<SignalMessage>? OnSignalReceived;

    public SignalRService(IConfiguration configuration)
    {
        //TODO: вынести в конфиг
        _hubUrl = "http://localhost:5056/signaling";
    }

    public async Task StartAsync()
    {
        if (_hubConnection is not null)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.SkipNegotiation = true;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
            })
            .Build();

        _hubConnection.On<string>("Registered", userId =>
        {
            _userId = userId;
            OnRegistered?.Invoke(userId);
        });

        _hubConnection.On<SignalMessage>("ReceiveSignal", signal =>
        {
            OnSignalReceived?.Invoke(signal);
        });

        await _hubConnection.StartAsync();
    }

    public async Task Register()
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("Connection not started");

        await _hubConnection.InvokeAsync("Register");
    }

    public async Task SendSignalAsync(string targetUserId, SignalMessage signal)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("Connection not started");

        await _hubConnection.InvokeAsync("RelaySignal", targetUserId, signal);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}