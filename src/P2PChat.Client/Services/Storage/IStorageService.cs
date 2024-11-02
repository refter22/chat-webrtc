namespace P2PChat.Client.Services.Storage;

public interface IStorageService
{
    Task<T?> LoadAsync<T>(string key);
    Task SaveAsync<T>(string key, T data);
    Task RemoveAsync(string key);
}