using System.Threading.Channels;
using Broker;
using Grpc.Core;
using MiniBroker.Worker.Data.Cache;
using MiniBroker.Worker.Data.Persistance;
using MiniBroker.Worker.Data.Repository;

namespace MiniBroker.Worker.Services;

public class MessageService(
    IConnectionRepository connectionRepository,
    IConnectionService service,
    IPersistanceService persistanceService)
{
    public void Disconnect(string clientId)
    {
        service.RemoveClient(clientId);
        connectionRepository.Disconnect(clientId);
    }

    public void Connect(string name,ServerCallContext context)
    {
        var uri = GetCallerUri(context);
        connectionRepository.AddToConnected(name, uri.AbsoluteUri);
    }
    
    private Uri GetCallerUri(ServerCallContext context)
    {
        var client = context.GetHttpContext();
        var clientIp = client.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var clientPort = client.Connection.RemotePort;

        if (clientIp == "::1")
            clientIp = "localhost";

        var scheme = "http";
        return new Uri($"{scheme}://{clientIp}:{clientPort}");
    }
    
    public void TryAddMessageToQueue(MessageStreamProto request)
    {
        if (!request.Context.PersistMessage)
            return;
        
        persistanceService.SaveMessage(request);
        
        service.AddMessage(request);
            
    }
    
    public void TryAddMessageToFailQueue(MessageStreamProto request)
    {
        if (!request.Context.PersistMessage)
            return;
        
        persistanceService.SaveMessage(request);
        
        service.AddMessage(request);
            
    }

    public Channel<MessageStreamProto>? TryGetChannel(string clientId)
    {
        return service.TryGetChanel(clientId);
    }
    
    public void RemoveMessageFromQueue(MessageStreamProto request)
    {
        if (!request.Context.PersistMessage)
            return;
        
        persistanceService.RemoveMessage(request);
    }
}