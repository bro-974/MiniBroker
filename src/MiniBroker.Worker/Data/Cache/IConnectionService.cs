using System.Threading.Channels;
using Broker;
using Grpc.Core;

namespace MiniBroker.Worker.Data.Cache;

public interface IConnectionService
{
    public string AddClient(IAsyncStreamReader<NotificationRequest> streamReader,IServerStreamWriter<MessageStreamProto> streamWriter,CancellationTokenSource cs);
    public IServerStreamWriter<MessageStreamProto>? TryGetClientStreamWriter(string clientId);
    public Channel<MessageStreamProto>? TryGetChanel(string clientId);
    void RemoveClient(string clientId);
    void AddMessage(MessageStreamProto request);
}