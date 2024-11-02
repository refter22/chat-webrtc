using Microsoft.JSInterop;
using P2PChat.Client.Services.WebRTC;

namespace P2PChat.Client.Models;

public class WebRTCState
{
    public string? TargetUserId { get; set; }
    public bool IsInitiator { get; set; }
    public bool IsConnected { get; set; }
    public bool IsInitialized { get; set; }
    public DotNetObjectReference<WebRTCService>? DotNetRef { get; set; }
}