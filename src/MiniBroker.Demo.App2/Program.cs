using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBroker.Abstraction;
using MiniBroker.Demo.App2.Messages;
using MiniBroker.Grpc.Client;
using MiniBroker.Demo.App2.Client.SourceGenerated;

namespace MiniBroker.Demo.App2;

internal class Program
{
    private const string Address = "http://localhost:5225";
    private const string ServiceName = "c2";
    private const string ServiceNameDest = "c1";

    private static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddMiniBrokerWorker(opt =>
                {
                    opt.Host = Address;
                    opt.ServiceName = ServiceName;
                    opt.EnableTypeSearchByName = true;
                });

                services.RegisterMiniBrokerSourceHandlers();
                
                // Optional: Logging configuration
                services.AddLogging(config =>
                {
                    config.ClearProviders();
                    // config.AddConsole();
                    // config.AddDebug();
                });
            })
            .Build();

        // Start the host, which triggers StartAsync on ListenerHostedService
        await host.StartAsync();

        var endpoint = host.Services.GetRequiredService<IEndpointProvider>();

        Console.WriteLine("Client Name: " + ServiceName);
        Console.WriteLine("Send text to: " + ServiceNameDest);
        Console.WriteLine("Type 'exit' to quit.");
        Console.WriteLine("Message:");

        string? message;
        do
        {
            message = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(message) && message != "exit")
            {
                endpoint.Send(ServiceNameDest, new ChatMessage(message));
            }
        } while (message != "exit");

        await host.StopAsync();
    }
}