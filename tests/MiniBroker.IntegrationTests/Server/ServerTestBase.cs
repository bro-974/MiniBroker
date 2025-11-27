using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBroker.Abstraction;
using MiniBroker.Grpc.Client;
using MiniBroker.Worker;

namespace MiniBroker.IntegrationTests.Server;

public abstract class ServerTestBase
{
    protected static readonly ConcurrentQueue<string> MessagesReceived = new();
    private readonly string _hostAddress = "https://localhost:5005";

    [TestInitialize]
    public async Task StartServer()
    {
        // Use the new helper to build the app
        var app = CreateApp(url: _hostAddress);

        _ = app.RunAsync(); // fire-and-forget

        // Wait briefly to allow server to start
        await Task.Delay(500);
    }

    protected IHostBuilder GetClientBuilder(string clientId)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMiniBrokerWorker(opt =>
                {
                    opt.Host = _hostAddress;
                    opt.ServiceName = clientId;
                    opt.EnableTypeSearchByName = true;
                });

                services
                    .AddSingleton<IHandleMessage<SystemNotificationMessage>,
                        TestSystemNotificationHandler>();
                services.AddLogging();
            });
    }

    public class TestSystemNotificationHandler : IHandleMessage<SystemNotificationMessage>
    {
        public Task OnMessage(SystemNotificationMessage message, Context context)
        {
            MessagesReceived.Enqueue(message.Message);
            return Task.CompletedTask;
        }
        
        public Task OnMessage(object message, Context context)
        {
            return OnMessage((SystemNotificationMessage)message, context);
        }
    }
    
    private static WebApplication CreateApp(string[]? args = null, string? url = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);

        // Set the URL(s) to listen on
        if (!string.IsNullOrEmpty(url))
        {
            builder.WebHost.UseUrls(url);
        }
        
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddGrpc();
        builder.Services.RegisterMiniBroker();
        
        builder.Services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddConsole();
            config.AddDebug();
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapMiniBroker();

        return app;
    }
}