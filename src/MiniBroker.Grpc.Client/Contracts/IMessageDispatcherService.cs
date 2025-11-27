using Broker;

namespace MiniBroker.Grpc.Client.Contracts;

public interface IMessageDispatcherService
{
    void ProcessMessage(MessageStreamProto response);
    IEnumerable<string> GetSubscribedTypes();
}