using System.Collections.Concurrent;
using Broker;
using Grpc.Core;
using MiniBroker.Worker.Data.Cache;

namespace MiniBroker.Worker.Data.Repository;

public class ConnectionService : IConnectionRepository
{
    private Dictionary<string, ConnectionDto> Connections { get; set; } = new();

    public ConcurrentDictionary<string, IServerStreamWriter<MessageStreamProto>> Clients { get; set; } = new();

    public ConnectionDto AddToConnected(string name, string uri)
    {
        if (Connections.TryGetValue(name, out var connection))
            connection.IsConnected = true;
        else
            Connections.TryAdd(name, new ConnectionDto(name, uri) { IsConnected = true });

        return Connections[name];
    }

    public ConnectionDto Disconnect(string name)
    {
        if (Connections.TryGetValue(name, out var connection)) connection.IsConnected = false;
        return Connections[name];
    }

    public IEnumerable<ConnectionDto> GetConnected()
    {
        return Connections.Where(w => w.Value.IsConnected).Select(s => s.Value);
    }
}