using Broker;

namespace MiniBroker.Grpc.Client.Services;

public interface IBrokerConnector
{
    string GetClientId();
    Registry.RegistryClient GetClient();
    Task StartListeningAsync(string address, string clientId, CancellationToken cancellationToken);
    Task StopListeningAsync();
}