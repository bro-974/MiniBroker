using MiniBroker.Worker.Data.Persistance;

namespace MiniBroker.Worker.Services;

public class EventService(IPersistanceService persistanceService,MessageService messageService)
{
    public void OnStartup()
    {
        var pendingMessages = persistanceService.LoadMessages();
        foreach (var message in pendingMessages)
        {
            messageService.TryAddMessageToQueue(message);
        }
    }
}