using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using P2PChat.Client.Models;
using System.Text.Json;

namespace P2PChat.Client.Services.WebRTC;

internal class FileTransferManager
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;
    private readonly Dictionary<string, FileTransferState> _transfers = new();
    private const int ChunkSize = 16384; // 16KB chunks

    private class FileTransferState
    {
        public FileMetadata? Metadata { get; set; }
        public List<byte[]> Chunks { get; set; } = new();
        public int TotalChunks { get; set; }
    }

    public FileTransferManager(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task SendFile(string targetUserId, IBrowserFile file)
    {
        try
        {
            var metadata = new FileMetadata
            {
                Name = file.Name,
                Size = file.Size,
                MimeType = file.ContentType
            };

            var success = await _jsRuntime.InvokeAsync<bool>("webrtc.sendData",
                targetUserId,
                JsonSerializer.Serialize(new { type = "file-start", metadata }));

            if (!success) throw new Exception("Failed to send file metadata");

            using var stream = file.OpenReadStream(maxAllowedSize: 100_000_000); // 100MB max
            var buffer = new byte[ChunkSize];
            var totalChunks = (int)Math.Ceiling(file.Size / (double)ChunkSize);
            var currentChunk = 0;

            while (currentChunk < totalChunks)
            {
                var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                var chunk = new FileChunk
                {
                    Data = Convert.ToBase64String(buffer, 0, bytesRead),
                    Index = currentChunk,
                    Total = totalChunks
                };

                success = await _jsRuntime.InvokeAsync<bool>("webrtc.sendData",
                    targetUserId,
                    JsonSerializer.Serialize(new { type = "file-chunk", chunk }));

                if (!success) throw new Exception($"Failed to send chunk {currentChunk}");

                currentChunk++;
            }

            success = await _jsRuntime.InvokeAsync<bool>("webrtc.sendData",
                targetUserId,
                JsonSerializer.Serialize(new { type = "file-end" }));

            if (!success) throw new Exception("Failed to send file end signal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending file {FileName}", file.Name);
            throw;
        }
    }

    public void HandleFileStart(string targetUserId, FileMetadata metadata)
    {
        _transfers[targetUserId] = new FileTransferState
        {
            Metadata = metadata,
            Chunks = new List<byte[]>(),
            TotalChunks = 0
        };
    }

    public void HandleFileChunk(string targetUserId, FileChunk chunk)
    {
        if (!_transfers.TryGetValue(targetUserId, out var state))
        {
            throw new InvalidOperationException("File transfer not initialized");
        }

        var data = Convert.FromBase64String(chunk.Data);

        while (state.Chunks.Count <= chunk.Index)
        {
            state.Chunks.Add(null!);
        }

        state.Chunks[chunk.Index] = data;
        state.TotalChunks = chunk.Total;
    }

    public void HandleFileEnd(string targetUserId)
    {
        if (!_transfers.TryGetValue(targetUserId, out var state))
        {
            throw new InvalidOperationException("No file transfer in progress");
        }

        if (state.Chunks.Count != state.TotalChunks)
        {
            throw new InvalidOperationException($"Missing chunks. Expected {state.TotalChunks}, got {state.Chunks.Count}");
        }

        var completeFile = state.Chunks.SelectMany(chunk => chunk).ToArray();

        _transfers.Remove(targetUserId);
    }
}