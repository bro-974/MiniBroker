using System.Text.Json;
using Broker;
using MiniBroker.Abstraction;
using MiniBroker.Worker.Helpers;

namespace MiniBroker.Worker.Data.Persistance;

public class PersistanceService : IPersistanceService
{
    private const string MessageFilePath = "PendingMessages.json";
    private const string MessageFailFilePath = "PendingMessages.json";
    private readonly object _fileLock = new();

    public void SaveMessage(MessageStreamProto message)
    {
        if (message.Context?.PersistMessage is false)
            return;

        lock (_fileLock)
        {
            var serializedMessage = JsonSerializer.Serialize(message.ToMessage());
            File.AppendAllText(MessageFilePath, serializedMessage + Environment.NewLine);
        }
    }
    
    public void SaveFailMessage(MessageStreamProto message)
    {
        if (message.Context?.PersistMessage is false)
            return;

        lock (_fileLock)
        {
            var serializedMessage = JsonSerializer.Serialize(message.ToMessage());
            File.AppendAllText(MessageFailFilePath, serializedMessage + Environment.NewLine);
        }
    }

    public IEnumerable<MessageStreamProto> LoadMessages()
    {
        if (!File.Exists(MessageFilePath))
            return [];

        lock (_fileLock)
        {
            var lines = File.ReadAllLines(MessageFilePath);
            return lines.Select(l => JsonSerializer.Deserialize<MessageStream>(l)!.ToProto());
        }
    }

    public void RemoveMessage(MessageStreamProto processedMessage)
    {
        lock (_fileLock)
        {
            var lines = File.ReadAllLines(MessageFilePath);

            var remainingMessages = lines.Where(line =>
                JsonSerializer.Deserialize<MessageStream>(line)?.Id != processedMessage.Id);

            File.WriteAllLines(MessageFilePath, remainingMessages);
        }
    }
}