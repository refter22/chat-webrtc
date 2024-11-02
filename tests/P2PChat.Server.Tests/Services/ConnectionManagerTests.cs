using Microsoft.Extensions.Logging;
using Moq;
using P2PChat.Server.Services;
using Xunit;

namespace P2PChat.Server.Tests.Services;

public class ConnectionManagerTests
{
    private readonly Mock<ILogger<ConnectionManager>> _loggerMock;
    private readonly ConnectionManager _manager;

    public ConnectionManagerTests()
    {
        _loggerMock = new Mock<ILogger<ConnectionManager>>();
        _manager = new ConnectionManager(_loggerMock.Object);
    }

    [Fact]
    public void AddConnection_WhenCalled_StoresConnection()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "connection1";

        // Act
        _manager.AddConnection(userId, connectionId);

        // Assert
        Assert.Equal(connectionId, _manager.GetConnectionId(userId));
        Assert.Equal(userId, _manager.GetUserId(connectionId));
    }

    [Fact]
    public void AddConnection_WhenUserIdExists_UpdatesConnection()
    {
        // Arrange
        var userId = "user1";
        var oldConnectionId = "connection1";
        var newConnectionId = "connection2";

        // Act
        _manager.AddConnection(userId, oldConnectionId);
        _manager.AddConnection(userId, newConnectionId);

        // Assert
        Assert.Equal(newConnectionId, _manager.GetConnectionId(userId));
        Assert.Equal(userId, _manager.GetUserId(newConnectionId));
        Assert.Null(_manager.GetUserId(oldConnectionId));
    }

    [Fact]
    public void AddConnection_WhenConnectionIdExists_UpdatesUser()
    {
        // Arrange
        var oldUserId = "user1";
        var newUserId = "user2";
        var connectionId = "connection1";

        // Act
        _manager.AddConnection(oldUserId, connectionId);
        _manager.AddConnection(newUserId, connectionId);

        // Assert
        Assert.Equal(connectionId, _manager.GetConnectionId(newUserId));
        Assert.Equal(newUserId, _manager.GetUserId(connectionId));
        Assert.Null(_manager.GetConnectionId(oldUserId));
    }

    [Fact]
    public void RemoveConnection_WhenExists_RemovesConnection()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "connection1";
        _manager.AddConnection(userId, connectionId);

        // Act
        _manager.RemoveConnection(connectionId);

        // Assert
        Assert.Null(_manager.GetConnectionId(userId));
        Assert.Null(_manager.GetUserId(connectionId));
    }

    [Fact]
    public void RemoveConnection_WhenNotExists_DoesNothing()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "connection1";
        _manager.AddConnection(userId, connectionId);

        // Act
        _manager.RemoveConnection("nonexistent");

        // Assert
        Assert.Equal(connectionId, _manager.GetConnectionId(userId));
        Assert.Equal(userId, _manager.GetUserId(connectionId));
    }
}