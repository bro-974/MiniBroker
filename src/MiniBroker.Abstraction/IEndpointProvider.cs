using System;

namespace MiniBroker.Abstraction;

public interface IEndpointProvider
{
    void Send<T>(string dest, T message, Action<MessageOption>? option = null);
}

public class MessageOption
{
    public enum PersistanceEnum
    {
        None,
        Keep
    }

    //public TimeSpan Expiration { get; set; } = TimeSpan.Zero;
    public PersistanceEnum Persistance { get; set; } = PersistanceEnum.Keep;
}