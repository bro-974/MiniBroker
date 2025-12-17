using Broker;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniBroker.Abstraction;
using MiniBroker.Grpc.Client.Configuration;
using MiniBroker.Grpc.Client.Contracts;
using MiniBroker.Grpc.Client.Helpers;

namespace MiniBroker.Grpc.Client.Services;

public class MessageDispatcherService(
    ILogger<MessageDispatcherService> logger,
    IServiceProvider provider,
    ISerializerProvider serializerProvider,
    IOptions<MiniBrokerOptions> option) : IMessageDispatcherService
{
    private readonly Dictionary<string, Type> _messageTypes = new();

    /// <summary>
    ///     This method should return the types of message the client as implemented with the IHandleMessage.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> GetSubscribedTypes()
    {
        var handlerInterfaceType = typeof(IHandleMessage<>);
        // Loop over all registered services in the provider
        foreach (var service in provider.GetServices<IHandleMessage>())
        {
            var serviceType = service.GetType();

            // Get all interfaces this service implements
            var interfaces = serviceType.GetInterfaces();

            foreach (var iface in interfaces)
            {
                // Check if it’s a generic IHandleMessage<>
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != handlerInterfaceType)
                    continue;

                var messageType = iface.GetGenericArguments()[0];
                if (messageType.FullName == null)
                    continue;

                var name = option.Value.EnableTypeSearchByName
                    ? messageType.Name
                    : messageType.FullName;

                //Save the type in the dictionary for later use
                if (!_messageTypes.ContainsKey(name!)) _messageTypes[name!] = messageType;
            }
        }

        return _messageTypes.Keys;
    }


    public void ProcessMessage(MessageStreamProto response)
    {
        logger.LogInformation("Received message Type: {MessageType}", response.Type);
        if (string.IsNullOrEmpty(response.Type))
        {
            logger.LogError("missing Type");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(response.Type))
        {
            logger.LogError("Received message with missing Type field.");
            return;
        }
        
        var messageType = option.Value.EnableTypeSearchByName
            ? ResolveTypeByNameOrFullName(response.Type)
            : ResolveTypeByFullName(response.Type);

        if (messageType == null)
        {
            logger.LogError("Unknown type {MessageType}", response.Type);
            return;
        }

        if (response.Payload == ByteString.Empty)
        {
            logger.LogError("Received message with missing Payload field.");
            return;
        }

        if (response.Context == null)
        {
            logger.LogError("Received message with missing Context field.");
            return;
        }

        var rawMessage = response.Payload.ToByteArray();
        if (!serializerProvider.TryDeserialize(rawMessage, messageType, out var instance))
        {
            logger.LogError("Failed to deserialize message of type {MessageType}", messageType.FullName);
            return;
        }

        var context = response.Context.ToMessage();
        DispatchHandlerForInstance(messageType, instance!, context);
    }

    private void DispatchHandlerForInstance(Type messageType, object instance, Context context)
    {
        // Resolve the service
        var listener = provider.GetKeyedService<IHandleMessage>(messageType.FullName);
        if (listener == null)
        {
            logger.LogError("No listener found for type {MessageType}.", messageType);
            return;
        }

        listener.OnMessage(instance, context);
    }

    private Type? ResolveTypeByFullName(string fullName)
    {
        if (_messageTypes.TryGetValue(fullName, out var name))
            return name;

        // Search all loaded assemblies for the type
        var type = Type.GetType(fullName); // works if assembly-qualified
        if (type != null)
        {
            _messageTypes[fullName] = type;
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            //a voir pour rechercher par Name!
            type = assembly.GetType(fullName);
            if (type != null)
            {
                _messageTypes[fullName] = type;
                return type;
            }
        }

        return null; // Type not found
    }

    private Type? ResolveTypeByNameOrFullName(string nameOrFullName)
    {
        if (_messageTypes.TryGetValue(nameOrFullName, out var cachedType))
            return cachedType;

        // Try Type.GetType for assembly-qualified or full name
        var type = Type.GetType(nameOrFullName);
        if (type != null)
        {
            _messageTypes[nameOrFullName] = type;
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Search by FullName
            type = assembly.GetType(nameOrFullName);
            if (type != null)
            {
                _messageTypes[nameOrFullName] = type;
                return type;
            }

            // Search by Name (simple name)
            type = assembly.GetTypes().FirstOrDefault(t => t.Name == nameOrFullName);
            if (type != null)
            {
                _messageTypes[nameOrFullName] = type;
                return type;
            }
        }

        return null; // Not found
    }
}