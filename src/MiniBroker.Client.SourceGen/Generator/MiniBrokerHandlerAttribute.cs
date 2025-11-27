using System;

namespace MiniBroker.Client.SourceGen.Generator;

public sealed class MiniBrokerHandlerAttribute : Attribute
{
    public enum MiniBrokerLifetime
    {
        Transient,
        Scoped,
        Singleton
    }

    public MiniBrokerLifetime Lifetime { get; set; } = MiniBrokerLifetime.Transient;
}