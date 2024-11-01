using Microsoft.JSInterop;
using P2PChat.Client.Services;

namespace P2PChat.Client.Services;

public class WebRTCState
{
    public string? TargetUserId { get; set; }
    public bool IsInitiator { get; set; }
    public bool IsConnected { get; set; }
    public bool IsInitialized { get; set; }
    public DotNetObjectReference<WebRTCService>? DotNetRef { get; set; }
}