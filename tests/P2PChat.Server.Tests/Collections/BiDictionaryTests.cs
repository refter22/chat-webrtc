using P2PChat.Server.Collections;
using Xunit;

namespace P2PChat.Server.Tests.Collections;

public class BiDictionaryTests
{
    private readonly BiDictionary<string, string> _dictionary;

    public BiDictionaryTests()
    {
        _dictionary = new BiDictionary<string, string>();
    }

    [Fact]
    public void Add_WhenNewPair_StoresValues()
    {
        // Arrange
        var key = "key1";
        var value = "value1";

        // Act
        _dictionary.Add(key, value);

        // Assert
        Assert.True(_dictionary.TryGetValue(key, out var storedValue));
        Assert.True(_dictionary.TryGetByValue(value, out var storedKey));
        Assert.Equal(value, storedValue);
        Assert.Equal(key, storedKey);
    }

    [Fact]
    public void Add_WhenKeyExists_UpdatesValue()
    {
        // Arrange
        var key = "key1";
        var value1 = "value1";
        var value2 = "value2";

        // Act
        _dictionary.Add(key, value1);
        _dictionary.Add(key, value2);

        // Assert
        Assert.True(_dictionary.TryGetValue(key, out var storedValue));
        Assert.Equal(value2, storedValue);
        Assert.False(_dictionary.TryGetByValue(value1, out _));
    }

    [Fact]
    public void Add_WhenValueExists_UpdatesKey()
    {
        // Arrange
        var key1 = "key1";
        var key2 = "key2";
        var value = "value1";

        // Act
        _dictionary.Add(key1, value);
        _dictionary.Add(key2, value);

        // Assert
        Assert.True(_dictionary.TryGetByValue(value, out var storedKey));
        Assert.Equal(key2, storedKey);
        Assert.False(_dictionary.TryGetValue(key1, out _));
    }

    [Fact]
    public void RemoveByValue_WhenExists_RemovesBothMappings()
    {
        // Arrange
        var key = "key1";
        var value = "value1";
        _dictionary.Add(key, value);

        // Act
        var result = _dictionary.RemoveByValue(value);

        // Assert
        Assert.True(result);
        Assert.False(_dictionary.TryGetValue(key, out _));
        Assert.False(_dictionary.TryGetByValue(value, out _));
    }

    [Fact]
    public void RemoveByValue_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = _dictionary.RemoveByValue("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryGetValue_WhenKeyNotExists_ReturnsFalse()
    {
        // Act
        var result = _dictionary.TryGetValue("nonexistent", out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryGetByValue_WhenValueNotExists_ReturnsFalse()
    {
        // Act
        var result = _dictionary.TryGetByValue("nonexistent", out _);

        // Assert
        Assert.False(result);
    }
}