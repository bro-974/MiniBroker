using System.Threading.Tasks;

namespace MiniBroker.Abstraction;

public interface IHandleMessage
{
    Task OnMessage(object message, Context context);
}

public interface IHandleMessage<in T>:IHandleMessage
{
    Task OnMessage(T message, Context context);
}