using Microsoft.Extensions.Logging;
using MiniBroker.Abstraction;
using MiniBroker.Client.SourceGen.Generator;

namespace MiniBroker.Demo.App1.Handlers;

[MiniBrokerHandler]
public partial class SystemNotificationHandler : IHandleMessage<SystemNotificationMessage>
{
    private readonly ILogger<SystemNotificationHandler> _logger;

    public SystemNotificationHandler(ILogger<SystemNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task OnMessage(SystemNotificationMessage message, Context context)
    {
        Console.WriteLine($"{context.Source} => {message.Message}");
        return Task.CompletedTask;
    }
}