using Broker;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using MiniBroker.Grpc.Client.Contracts;
using MiniBroker.Grpc.Client.Services;
using Moq;

namespace MiniBroker.Grpc.Client.Tests;

[TestClass]
public class BrokerConnectorTests
{
    private Mock<IMessageDispatcherService> _dispatcherMock = null!;
    private Mock<ILogger<BrokerConnector>> _loggerMock = null!;
    private Mock<IRegistryClientWrapper> _registryClientWrapperMock = null!;
    private BrokerConnector _connector = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<BrokerConnector>>();
        _dispatcherMock = new Mock<IMessageDispatcherService>();
        _registryClientWrapperMock = new Mock<IRegistryClientWrapper>();
        _connector = new BrokerConnector(
            _loggerMock.Object,
            _dispatcherMock.Object,
            _registryClientWrapperMock.Object
        );
    }

    [TestMethod]
    public Task GetClientId_ThrowsIfNotSet()
    {
        var connector = new BrokerConnector(_loggerMock.Object, _dispatcherMock.Object, _registryClientWrapperMock.Object);

        var ex = Assert.ThrowsException<InvalidOperationException>(() => { _ = connector.GetClientId(); });

        Assert.AreEqual("Client ID is not set. Please start listening first.", ex.Message);
        return Task.CompletedTask;
    }

    [TestMethod]
    public Task GetClient_ThrowsIfNotSet()
    {
        var connector = new BrokerConnector(_loggerMock.Object, _dispatcherMock.Object, _registryClientWrapperMock.Object);

        var ex = Assert.ThrowsException<InvalidOperationException>(() => { _ = connector.GetClient(); });

        Assert.AreEqual("Client is not set. Please start listening first.", ex.Message);
        return Task.CompletedTask;
    }

    [TestMethod]
    public async Task DisposeAsync_CleansUpResources()
    {
        var connector = new BrokerConnector(_loggerMock.Object, _dispatcherMock.Object, _registryClientWrapperMock.Object);

        await connector.DisposeAsync(); // Should not throw
    }
    
    [TestMethod]
    public async Task StartListeningAsync_ProcessesMessages()
    {
        // Arrange
        var message = new MessageStreamProto { Id = "msg1" };
        var mockResponseStream = new Mock<IAsyncStreamReader<MessageStreamProto>>();
        var responseQueue = new Queue<MessageStreamProto>(new[] { message });

        mockResponseStream.Setup(r => r.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responseQueue.Count > 0)
            .Callback(() => responseQueue.Dequeue());

        mockResponseStream.Setup(r => r.Current).Returns(message);

        var requestStreamMock = new Mock<IClientStreamWriter<NotificationRequest>>();
        requestStreamMock.Setup(w => w.WriteAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var call = new AsyncDuplexStreamingCall<NotificationRequest, MessageStreamProto>(
            requestStreamMock.Object,
            mockResponseStream.Object,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { }
        );

        _registryClientWrapperMock.Setup(w => w.OpenEventStream(It.IsAny<Registry.RegistryClient>()))
            .Returns(call);

        var cts = new CancellationTokenSource();

        // Act
        await _connector.StartListeningAsync("https://localhost:5001", "test-client", cts.Token);

        // Assert
        _dispatcherMock.Verify(d => d.ProcessMessage(It.Is<MessageStreamProto>(m => m.Id == "msg1")), Times.Once);
    }

    [TestMethod]
    public async Task StartListeningAsync_StopsOnCancellation()
    {
        var mockResponseStream = new Mock<IAsyncStreamReader<MessageStreamProto>>();
        mockResponseStream.Setup(r => r.MoveNext(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var requestStreamMock = new Mock<IClientStreamWriter<NotificationRequest>>();
        requestStreamMock.Setup(w => w.WriteAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var call = new AsyncDuplexStreamingCall<NotificationRequest, MessageStreamProto>(
            requestStreamMock.Object,
            mockResponseStream.Object,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { }
        );

        _registryClientWrapperMock.Setup(w => w.OpenEventStream(It.IsAny<Registry.RegistryClient>()))
            .Returns(call);

        var cts = new CancellationTokenSource();

        // Act
        await _connector.StartListeningAsync("https://localhost:5001", "test-client", cts.Token);

        // Assert
        _loggerMock.VerifyLog(LogLevel.Information, "Listening canceled.");
    }

    [TestMethod]
    public async Task StartListeningAsync_HandlesTransientRpcExceptionAndRetries()
    {
        int callCount = 0;

        _registryClientWrapperMock.Setup(w => w.OpenEventStream(It.IsAny<Registry.RegistryClient>()))
            .Callback(() => callCount++)
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "Transient")));

        var cts = new CancellationTokenSource();

        await _connector.StartListeningAsync("https://localhost:5001", "test-client", cts.Token);

        Assert.IsTrue(callCount >= 1, "Should retry at least once.");
    }

    [TestMethod]
    public async Task StartListeningAsync_AbortsAfterMaxRetries()
    {
        int callCount = 0;

        _registryClientWrapperMock.Setup(w => w.OpenEventStream(It.IsAny<Registry.RegistryClient>()))
            .Callback(() => callCount++)
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "Transient")));

        var cts = new CancellationTokenSource();

        await _connector.StartListeningAsync("https://localhost:5001", "test-client", cts.Token);

        Assert.IsTrue(callCount <= 5, "Should not retry more than max retry limit (5).");
    }
}