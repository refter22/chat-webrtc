namespace P2PChat.Client.Models;

public class ChatMessage
{
    public string? Text { get; set; }
    public bool IsFromMe { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFile { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string? FileMimeType { get; set; }
    public string? FileUrl { get; set; }
    public bool IsReceiving { get; set; }
    public int Progress { get; set; }
}
