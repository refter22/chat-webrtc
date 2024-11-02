namespace P2PChat.Client.Models;

public class FileTransferState
{
    public FileMetadata Metadata { get; set; } = null!;
    public List<string?> Chunks { get; set; } = new();
    public ChatMessage Message { get; set; } = null!;
    public int TotalChunks { get; set; }
}