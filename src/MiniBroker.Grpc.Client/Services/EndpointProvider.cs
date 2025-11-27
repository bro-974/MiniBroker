using System.Text.Json;
using Broker;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using MiniBroker.Abstraction;

namespace MiniBroker.Grpc.Client.Services;

internal class EndpointProvider(IBrokerConnector listener, ILogger<EndpointProvider> logger) : IEndpointProvider
{
    public void Send<T>(string dest, T message, Action<MessageOption>? option = null)
    {
        var ms = FormatMessage(dest, message, option);
        try
        {
            listener.GetClient().Send(ms, new CallOptions());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }
    
    public async Task SendAsync<T>(string dest, T message, Action<MessageOption>? option = null,
        CancellationToken ct = default)
    {
        var ms = FormatMessage(dest, message, option);

        try
        {
            await listener.GetClient().SendAsync(ms, null, null, ct);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }

    private MessageStreamProto FormatMessage<T>(string dest, T message, Action<MessageOption>? option = null)
    {
        var messageOption = new MessageOption();
        option?.Invoke(messageOption);

        if (message == null)
            throw new InvalidOperationException("Message must be not null");

        if (dest == null)
            throw new InvalidOperationException("dest must be not null");

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        return new MessageStreamProto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = typeof(T).Name,
            Payload = ByteString.CopyFrom(jsonBytes),
            Context = new ContextProto
            {
                Destinataire = dest,
                Source = listener.GetClientId(),
                PersistMessage = messageOption.Persistance == MessageOption.PersistanceEnum.Keep
            }
        };
    }

    
}