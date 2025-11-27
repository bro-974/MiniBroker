using System.Text.Json;
using MiniBroker.Grpc.Client.Contracts;

namespace MiniBroker.Grpc.Client.Services;

public class JsonSerializerProvider : ISerializerProvider
{
    public bool TryDeserialize(byte[] payload, Type type, out object? instance)
    {
        instance = JsonSerializer.Deserialize(payload, type);
        if (instance != null) return true;
        return false;
    }
}