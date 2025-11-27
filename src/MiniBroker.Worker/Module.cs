using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MiniBroker.Worker.Data.Cache;
using MiniBroker.Worker.Data.Persistance;
using MiniBroker.Worker.Data.Repository;
using MiniBroker.Worker.Endpoints;
using MiniBroker.Worker.Rpc;
using MiniBroker.Worker.Services;
using ConnectionService = MiniBroker.Worker.Data.Cache.ConnectionService;

namespace MiniBroker.Worker
{
    public static class Module
    {
        public static IServiceCollection RegisterMiniBroker(this IServiceCollection services)
        {
            
            services.AddSingleton<IConnectionRepository, Data.Repository.ConnectionService>();
            services.AddSingleton<IConnectionService, ConnectionService>();
            services.AddSingleton<IPersistanceService, PersistanceService>();
            services.AddSingleton<MessageService>();
            services.AddSingleton<EventService>();
            return services;
        }
        
        public static WebApplication MapMiniBroker(this WebApplication app)
        {
            app.RegisterAllEndpoints();
            app.MapGrpcService<BrokerService>();
            app.Services.GetRequiredService<EventService>().OnStartup();
            return app;
        }
        
        public static IApplicationBuilder MapMiniBroker(this IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<BrokerService>();
            });

            var eventService = app.ApplicationServices.GetRequiredService<EventService>();
            eventService.OnStartup();

            return app;
        }
    }
}
