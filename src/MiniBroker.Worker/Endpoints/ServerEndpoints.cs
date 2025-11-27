using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniBroker.Worker.Data.Repository;

namespace MiniBroker.Worker.Endpoints;

public static class ServerEndpoints
{
    public static void RegisterAllEndpoints(this WebApplication app)
    {
        app.MapTokensEndpoints();
        app.MapTestEndpoints();
    }

    private static void MapTokensEndpoints(this WebApplication app)
    {
        app.MapGet("/server/Connected",  ([FromServices] IConnectionRepository repository) =>
        {
            var connected = repository.GetConnected();
            return Results.Ok(connected);
        });
    }

    private static void MapTestEndpoints(this WebApplication app)
    {
        app.MapGet("/server/ping", () => { return "Welcome"; });
    }
}