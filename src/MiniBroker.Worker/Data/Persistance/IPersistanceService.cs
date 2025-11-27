using Broker;

namespace MiniBroker.Worker.Data.Persistance;

public interface IPersistanceService
{
    void SaveMessage(MessageStreamProto message);
    
    void SaveFailMessage(MessageStreamProto message);

    IEnumerable<MessageStreamProto> LoadMessages();

    void RemoveMessage(MessageStreamProto processedMessage);
}