using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniBroker.Abstraction;
using MiniBroker.Grpc.Client.Configuration;
using MiniBroker.Grpc.Client.Contracts;
using MiniBroker.Grpc.Client.Services;

namespace MiniBroker.Grpc.Client;

public static class Module
{
    /// <summary>
    ///     Configure and start the MiniBroker worker to run as a background service to connect and receive message from the
    ///     server
    /// </summary>
    /// <param name="services"></param>
    /// <param name="option"></param>
    /// <returns></returns>
    public static IServiceCollection AddMiniBrokerWorker(this IServiceCollection services,
        Action<MiniBrokerOptions> option)
    {
        services.AddOptions<MiniBrokerOptions>()
            .Configure(option)
            .Validate(options => 
                !string.IsNullOrWhiteSpace(options.Host) &&
                !string.IsNullOrWhiteSpace(options.ServiceName),
            "Host and ServiceName must be provided.");
        
        services.RegisterInternal();
        services.AddHostedService<ListenerHostedService>();
        return services;
    }

    /// <summary>
    ///     Register all message handlers in the assembly
    /// </summary>
    /// <typeparam name="TAssembly"></typeparam>
    /// <param name="services"></param>
    public static void RegisterMessageInAssembly<TAssembly>(this IServiceCollection services)
    {
        services.RegisterInternal();
        services.RegisterMessage(typeof(TAssembly).Assembly, typeof(IHandleMessage<>), typeof(IHandleMessage));
    }

    
    private static void RegisterInternal(this IServiceCollection services)
    {
        services.TryAddSingleton<IBrokerConnector, BrokerConnector>();
        services.TryAddSingleton<IEndpointProvider, EndpointProvider>();
        services.TryAddSingleton<ISerializerProvider, JsonSerializerProvider>();
        services.TryAddSingleton<IMessageDispatcherService, MessageDispatcherService>();
        services.TryAddSingleton<IRegistryClientWrapper, RegistryClientWrapper>();
    }

    private static void RegisterMessage(this IServiceCollection services, Assembly assembly, Type interfaceType)
    {
        // Find all types in the assembly that implement the type
        var listenerTypes = assembly.GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType))
            .ToList();

        foreach (var listenerType in listenerTypes)
        {
            // Find all interfaces implemented by the class that are of type IHandleMessage<>
            var interfaces = listenerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleMessage<>));

            foreach (var inter in interfaces)
                // Register the class with its interface
                services.AddTransient(inter, listenerType);
        }
    }
    
    private static void RegisterMessage(this IServiceCollection services, Assembly assembly, Type interfaceType, Type interfaceType2)
    {
        var listenerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface &&
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType))
            .ToList();

        foreach (var implementationType in listenerTypes)
        {
            // Register the implementation as self (for internal reuse)
            services.AddTransient(implementationType);
            
            // Register all IHandleMessage<T> interfaces it implements
            var genericInterfaces = implementationType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);

            foreach (var concreteGenericInterface in genericInterfaces)
            {
                services.AddTransient(concreteGenericInterface, provider => provider.GetRequiredService(implementationType));
            }

            // Register the non-generic IHandleMessage interface
            if (interfaceType2.IsAssignableFrom(implementationType))
            {
                services.AddTransient(interfaceType2, provider => provider.GetRequiredService(implementationType));
            }
        }
    }
}