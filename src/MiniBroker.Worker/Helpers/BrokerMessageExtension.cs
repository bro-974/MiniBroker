using Broker;
using Google.Protobuf;
using MiniBroker.Abstraction;

namespace MiniBroker.Worker.Helpers;

public static class BrokerMessageExtension
{
    public static ContextProto ToProto(this Context message)
    {
        return new ContextProto
        {
            Source = message.Source,
            Destinataire = message.Destinataire,
            PersistMessage = message.PersistMessage
        };
    }

    public static Context ToMessage(this ContextProto message)
    {
        return new Context
        {
            Source = message.Source,
            Destinataire = message.Destinataire,
            PersistMessage = message.PersistMessage
        };
    }

    public static MessageStream ToMessage(this MessageStreamProto message)
    {
        return new MessageStream
        {
            Id = message.Id,
            Type = message.Type,
            Context = message.Context.ToMessage(),
            Payload = message.Payload.ToByteArray()
        };
    }

    public static MessageStreamProto ToProto(this MessageStream message)
    {
        return new MessageStreamProto
        {
            Id = message.Id,
            Type = message.Type,
            Context = message.Context?.ToProto(),
            Payload = ByteString.CopyFrom(message.Payload)
        };
    }
}