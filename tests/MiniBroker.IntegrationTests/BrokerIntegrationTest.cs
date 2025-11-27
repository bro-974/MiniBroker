using System.Collections.Concurrent;
using MiniBroker.Abstraction;
using MiniBroker.IntegrationTests.Server;

namespace MiniBroker.IntegrationTests;

[TestClass]
public class BrokerIntegrationTests : ServerTestBase
{
    [TestMethod]
    public async Task Client_Receives_SystemNotificationMessage_On_Connect()
    {
        using var clientHost = GetClientBuilder("test-client")
            .Build();

        await clientHost.StartAsync();
        await Task.Delay(1000); // Wait for message to arrive

        Assert.IsTrue(MessagesReceived.TryDequeue(out var message), "No message received from server.");
        Assert.IsTrue(message.Contains("Welcome"), $"Unexpected message: {message}");

        await clientHost.StopAsync();
    }
}