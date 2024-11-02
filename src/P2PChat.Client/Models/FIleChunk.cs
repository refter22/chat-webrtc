namespace P2PChat.Client.Models;

public class FileChunk
{
    public string Data { get; set; } = "";
    public int Index { get; set; }
    public int Total { get; set; }
}