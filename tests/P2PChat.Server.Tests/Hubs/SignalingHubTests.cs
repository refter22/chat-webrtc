using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using P2PChat.Server.Hubs;
using P2PChat.Server.Services.Interfaces;
using P2PChat.Shared.Models;
using Xunit;

namespace P2PChat.Server.Tests.Hubs;

public class SignalingHubTests
{
    private readonly Mock<IConnectionManager> _connectionManagerMock;
    private readonly Mock<ILogger<SignalingHub>> _loggerMock;
    private readonly Mock<IHubCallerClients<ISignalingClient>> _clientsMock;
    private readonly Mock<ISignalingClient> _callerMock;
    private readonly Mock<ISignalingClient> _clientMock;
    private readonly HubCallerContext _contextMock;
    private readonly SignalingHub _hub;

    public SignalingHubTests()
    {
        _connectionManagerMock = new Mock<IConnectionManager>();
        _loggerMock = new Mock<ILogger<SignalingHub>>();

        // Моки для SignalR
        _clientsMock = new Mock<IHubCallerClients<ISignalingClient>>();
        _callerMock = new Mock<ISignalingClient>();
        _clientMock = new Mock<ISignalingClient>();

        _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
        _clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_clientMock.Object);

        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns("testConnectionId");
        _contextMock = context.Object;

        _hub = new SignalingHub(_loggerMock.Object, _connectionManagerMock.Object)
        {
            Clients = _clientsMock.Object,
            Context = _contextMock
        };
    }

    [Fact]
    public async Task Register_WithoutExistingId_GeneratesNewId()
    {
        // Act
        await _hub.Register(null);

        // Assert
        _connectionManagerMock.Verify(
            m => m.AddConnection(
                It.Is<string>(id => !string.IsNullOrEmpty(id)),
                "testConnectionId"
            ),
            Times.Once
        );

        _callerMock.Verify(
            c => c.Registered(
                It.Is<string>(id => !string.IsNullOrEmpty(id))
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Register_WithExistingId_UsesProvidedId()
    {
        // Arrange
        var existingId = "existingId";

        // Act
        await _hub.Register(existingId);

        // Assert
        _connectionManagerMock.Verify(
            m => m.AddConnection(existingId, "testConnectionId"),
            Times.Once
        );

        _callerMock.Verify(
            c => c.Registered(existingId),
            Times.Once
        );
    }

    [Fact]
    public async Task RelaySignal_WhenFromUserExists_RelaysSignal()
    {
        // Arrange
        var fromUserId = "fromUser";
        var targetUserId = "targetUser";
        var targetConnectionId = "targetConnection";
        var signal = new SignalMessage { Type = SignalType.Offer };

        _connectionManagerMock
            .Setup(m => m.GetUserId("testConnectionId"))
            .Returns(fromUserId);

        _connectionManagerMock
            .Setup(m => m.GetConnectionId(targetUserId))
            .Returns(targetConnectionId);

        // Act
        await _hub.RelaySignal(targetUserId, signal);

        // Assert
        _clientMock.Verify(
            c => c.ReceiveSignal(
                It.Is<SignalMessage>(s =>
                    s.Type == SignalType.Offer &&
                    s.FromUserId == fromUserId
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task RelaySignal_WhenFromUserNotExists_DoesNotRelay()
    {
        // Arrange
        _connectionManagerMock
            .Setup(m => m.GetUserId("testConnectionId"))
            .Returns((string?)null);

        // Act
        await _hub.RelaySignal("targetUser", new SignalMessage { Type = SignalType.Offer });

        // Assert
        _clientMock.Verify(
            c => c.ReceiveSignal(It.IsAny<SignalMessage>()),
            Times.Never
        );
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesConnection()
    {
        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _connectionManagerMock.Verify(
            m => m.RemoveConnection("testConnectionId"),
            Times.Once
        );
    }
}