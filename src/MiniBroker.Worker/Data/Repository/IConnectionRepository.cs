using MiniBroker.Worker.Data.Cache;

namespace MiniBroker.Worker.Data.Repository;

public interface IConnectionRepository
{
    IEnumerable<ConnectionDto> GetConnected();
    ConnectionDto AddToConnected(string name, string uri);
    ConnectionDto Disconnect(string name);
}