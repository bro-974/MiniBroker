using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniBroker.Grpc.Client.Configuration;

namespace MiniBroker.Grpc.Client.Services;

public class ListenerHostedService(
    IBrokerConnector brokerConnector,
    IOptions<MiniBrokerOptions> options,
    ILogger<ListenerHostedService> logger) : IHostedService
{
    private readonly MiniBrokerOptions _options = options.Value;
    private Task? _backgroundTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Listener started for service '{ServiceName}'", _options.ServiceName);


        // Run the infinite loop in the background
        _backgroundTask = Task.Run(async () =>
        {
            try
            {
                await brokerConnector.StartListeningAsync(_options.Host, _options.ServiceName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Listener was cancelled.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in listener.");
                if(_options.ThrowOnConnectionFailure)
                    throw; // optionally rethrow if you want it to crash the host
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping listener for service '{ServiceName}'", _options.ServiceName);

        await brokerConnector.StopListeningAsync();
        
        if (_backgroundTask is not null)
        {
            await Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
        
        logger.LogInformation("Stopped listener for service '{ServiceName}'", _options.ServiceName);
    }
}