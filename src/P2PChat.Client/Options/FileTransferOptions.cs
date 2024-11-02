namespace P2PChat.Client.Options;

public class FileTransferOptions
{
    public const int DefaultChunkSize = 16384; // 16KB
    public const int DefaultMaxFileSize = 100 * 1024 * 1024; // 100MB
    public const int DefaultChunkDelay = 50; // ms

    public int ChunkSize { get; set; } = DefaultChunkSize;
    public int MaxFileSize { get; set; } = DefaultMaxFileSize;
    public int ChunkDelay { get; set; } = DefaultChunkDelay;
}