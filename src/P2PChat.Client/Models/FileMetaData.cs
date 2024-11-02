namespace P2PChat.Client.Models;

public class FileMetadata
{
    public string Name { get; set; } = null!;
    public long Size { get; set; }
    public string MimeType { get; set; } = null!;
}