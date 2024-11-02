using System.Diagnostics.CodeAnalysis;

namespace P2PChat.Server.Collections;

public class BiDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : notnull
{
    private readonly Dictionary<TKey, TValue> _forward = new();
    private readonly Dictionary<TValue, TKey> _reverse = new();

    public void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (_forward.TryGetValue(key, out var existingValue))
        {
            _forward.Remove(key);
            _reverse.Remove(existingValue);
        }
        if (_reverse.TryGetValue(value, out var existingKey))
        {
            _forward.Remove(existingKey);
            _reverse.Remove(value);
        }

        _forward.Add(key, value);
        _reverse.Add(value, key);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) =>
        _forward.TryGetValue(key, out value);

    public bool TryGetByValue(TValue value, [MaybeNullWhen(false)] out TKey key) =>
        _reverse.TryGetValue(value, out key);

    public bool RemoveByValue(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_reverse.TryGetValue(value, out var key))
        {
            _forward.Remove(key);
            _reverse.Remove(value);
            return true;
        }
        return false;
    }

    public IEnumerable<TKey> Keys => _forward.Keys;
}