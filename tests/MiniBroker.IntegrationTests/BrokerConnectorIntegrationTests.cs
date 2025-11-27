using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBroker.Grpc.Client.Contracts;
using MiniBroker.Grpc.Client.Services;
using MiniBroker.Worker;
using Moq;

namespace MiniBroker.IntegrationTests;

[TestClass]
public class BrokerConnectorIntegrationTests
{
    private GrpcChannel _channel = null!;
    private TestServer _server = null!;

    [TestInitialize]
    public void Setup()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddGrpc();
                services.RegisterMiniBroker(); // <-- your extension method
            })
            .Configure(app =>
            {
                app.MapMiniBroker(); // <-- your mapping method
            });

        _server = new TestServer(builder);
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = _server.CreateClient()
        });
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _channel.ShutdownAsync();
        _server.Dispose();
    }

    [TestMethod]
    public async Task BrokerConnector_ShouldConnectAndListen()
    {
        // Arrange
        var mockDispatcher = new Mock<IMessageDispatcherService>();
        var mockWrapper = new RegistryClientWrapper(); // You can use real implementation here
        var logger = new NullLogger<BrokerConnector>();

        var connector = new BrokerConnector(logger, mockDispatcher.Object, mockWrapper);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // auto cancel after 5s

        // Act
        await connector.StartListeningAsync("http://localhost", "test-client", cts.Token);

        // Assert
        var clientId = connector.GetClientId();
        Assert.AreEqual("test-client", clientId);
    }
}