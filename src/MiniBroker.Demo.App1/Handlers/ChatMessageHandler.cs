using MiniBroker.Abstraction;
using MiniBroker.Client.SourceGen.Generator;
using MiniBroker.Demo.App1.Messages;

namespace MiniBroker.Demo.App1.Handlers;

[MiniBrokerHandler]
public partial class ChatMessageHandler : IHandleMessage<ChatMessage>
{
    public Task OnMessage(ChatMessage message, Context context)
    {
        Console.WriteLine($"{context.Source} => {message.Message}");
        return Task.CompletedTask;
    }
}