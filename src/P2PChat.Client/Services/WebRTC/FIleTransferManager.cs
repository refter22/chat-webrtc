using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using P2PChat.Client.Models;
using System.Text.Json;

namespace P2PChat.Client.Services.WebRTC;

public class FileTransferManager
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<FileTransferManager> _logger;
    private readonly Dictionary<string, FileTransferState> _transfers = new();
    private const int ChunkSize = 16384; // 16KB chunks
    private const int MaxFileSize = 100 * 1024 * 1024; // 100MB

    private class FileTransferState
    {
        public FileMetadata Metadata { get; set; } = null!;
        public List<string?> Chunks { get; set; } = new();
        public ChatMessage Message { get; set; } = null!;
        public int TotalChunks { get; set; }
    }

    public event Action<string, ChatMessage> OnFileMessageCreated = null!;
    public event Action StateChanged = null!;

    public FileTransferManager(IJSRuntime jsRuntime, ILogger<FileTransferManager> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task SendFile(string targetUserId, IBrowserFile file, IJSObjectReference connection)
    {
        try
        {
            if (file.Size > MaxFileSize)
            {
                throw new InvalidOperationException($"File size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB");
            }

            var metadata = new FileMetadata
            {
                Name = file.Name,
                Size = file.Size,
                MimeType = file.ContentType
            };

            var message = new ChatMessage
            {
                IsFromMe = true,
                IsFile = true,
                FileName = file.Name,
                FileSize = file.Size,
                FileMimeType = file.ContentType,
                Timestamp = DateTime.Now
            };

            OnFileMessageCreated?.Invoke(targetUserId, message);

            var metadataJson = JsonSerializer.Serialize(new { type = "file-start", metadata });
            await SendData(connection, metadataJson);

            var buffer = new byte[ChunkSize];
            var totalChunks = (int)Math.Ceiling(file.Size / (double)ChunkSize);
            var currentChunk = 0;

            using var stream = file.OpenReadStream(maxAllowedSize: file.Size);

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                var chunk = new FileChunk
                {
                    Data = Convert.ToBase64String(buffer, 0, bytesRead),
                    Index = currentChunk,
                    Total = totalChunks
                };

                var chunkJson = JsonSerializer.Serialize(new { type = "file-chunk", chunk });
                await SendData(connection, chunkJson);

                message.Progress = (int)((currentChunk + 1.0) / totalChunks * 100);
                StateChanged?.Invoke();

                currentChunk++;
                if (currentChunk < totalChunks)
                    await Task.Delay(50);
            }

            using var ms = new MemoryStream();
            stream.Position = 0;
            await stream.CopyToAsync(ms);
            message.FileUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";

            var endJson = JsonSerializer.Serialize(new { type = "file-end" });
            await SendData(connection, endJson);

            message.Progress = 100;
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending file");
            throw;
        }
    }

    private async Task SendData(IJSObjectReference connection, string data)
    {
        var success = await _jsRuntime.InvokeAsync<bool>("webrtc.sendData", connection, data);
        if (!success)
        {
            throw new Exception("Failed to send data");
        }
    }

    public void HandleFileStart(string userId, FileMetadata metadata)
    {
        var message = new ChatMessage
        {
            IsFromMe = false,
            IsFile = true,
            FileName = metadata.Name,
            IsReceiving = true,
            Progress = 0,
            FileSize = metadata.Size,
            FileMimeType = metadata.MimeType,
            Timestamp = DateTime.Now
        };

        _transfers[userId] = new FileTransferState
        {
            Metadata = metadata,
            Message = message,
            Chunks = new List<string?>()
        };

        OnFileMessageCreated?.Invoke(userId, message);
        _logger.LogInformation($"Started receiving file: {metadata.Name}");
    }

    public void HandleFileChunk(string userId, FileChunk chunk)
    {
        if (!_transfers.TryGetValue(userId, out var state))
        {
            _logger.LogError("No file transfer in progress");
            return;
        }

        while (state.Chunks.Count <= chunk.Index)
        {
            state.Chunks.Add(null);
        }

        state.Chunks[chunk.Index] = chunk.Data;
        state.TotalChunks = chunk.Total;

        state.Message.Progress = (int)((double)state.Chunks.Count(c => c != null) / chunk.Total * 100);
        StateChanged?.Invoke();
    }

    public void HandleFileEnd(string userId)
    {
        if (!_transfers.TryGetValue(userId, out var state))
        {
            _logger.LogError("No file transfer in progress");
            return;
        }

        var base64Data = string.Concat(state.Chunks.Where(c => c != null));
        state.Message.FileUrl = $"data:{state.Message.FileMimeType};base64,{base64Data}";
        state.Message.IsReceiving = false;
        state.Message.Progress = 100;

        _transfers.Remove(userId);
        StateChanged?.Invoke();
    }
}