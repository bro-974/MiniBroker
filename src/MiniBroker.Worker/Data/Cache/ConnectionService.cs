using System.Collections.Concurrent;
using System.Threading.Channels;
using Broker;
using Grpc.Core;

namespace MiniBroker.Worker.Data.Cache;

public class ConnectionService : IConnectionService
{
    private ConcurrentDictionary<string, (IServerStreamWriter<MessageStreamProto>, CancellationTokenSource)>
        Clients { get; } = new();

    private ConcurrentDictionary<string, Channel<MessageStreamProto>> PendingMessages { get; } = new();

    public string AddClient(IAsyncStreamReader<NotificationRequest> streamreader,
        IServerStreamWriter<MessageStreamProto> streamWriter, CancellationTokenSource cs)
    {
        if (Clients.TryGetValue(streamreader.Current.ClientId, out var client))
        {
            client.Item2.Cancel();
            Clients.TryRemove(streamreader.Current.ClientId, out _);
        }

        Clients[streamreader.Current.ClientId] = (streamWriter, cs);
        return streamreader.Current.ClientId;
    }

    public IServerStreamWriter<MessageStreamProto>? TryGetClientStreamWriter(string clientId)
    {
        if (Clients.TryGetValue(clientId, out var streamWriter))
            return streamWriter.Item1;
        return null;
    }

    public void RemoveClient(string clientId)
    {
        Clients.TryRemove(clientId, out _);
    }

    public void AddMessage(MessageStreamProto request)
    {
        PendingMessages.AddOrUpdate(
            request.Context.Destinataire,
            _ =>
            {
                var channel = Channel.CreateUnbounded<MessageStreamProto>();
                channel.Writer.WriteAsync(request);
                return channel;
            },
            (_, channel) =>
            {
                channel.Writer.WriteAsync(request);
                return channel;
            });
    }

    public Channel<MessageStreamProto>? TryGetChanel(string clientId)
    {
        return PendingMessages.GetValueOrDefault(clientId);
    }
}