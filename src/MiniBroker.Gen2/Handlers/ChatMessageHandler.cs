using MiniBroker.Abstraction;
using MiniBroker.Client.SourceGen.Generator;
using MiniBroker.Demo.App2.Messages;

namespace MiniBroker.Gen2.Handlers;

[MiniBrokerHandler(Lifetime = MiniBrokerHandlerAttribute.MiniBrokerLifetime.Scoped)]
public partial class ChatMessageHandler: IHandleMessage<ChatMessage>
{
    public Task OnMessage(ChatMessage message, Context context)
    {
        Console.WriteLine($"{context.Source} => {message.Message}");
        return Task.CompletedTask;
    }
}