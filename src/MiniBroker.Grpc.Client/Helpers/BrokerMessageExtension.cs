using Broker;
using MiniBroker.Abstraction;

namespace MiniBroker.Grpc.Client.Helpers;

public static class BrokerMessageExtension
{
    public static Context ToMessage(this ContextProto message)
    {
        return new Context
        {
            Source = message.Source,
            Destinataire = message.Destinataire,
            PersistMessage = message.PersistMessage
        };
    }
    
}