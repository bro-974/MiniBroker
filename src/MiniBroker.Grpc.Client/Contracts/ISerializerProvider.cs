namespace MiniBroker.Grpc.Client.Contracts;

public interface ISerializerProvider
{
    bool TryDeserialize(byte[] payload, Type type, out object? instance);
}