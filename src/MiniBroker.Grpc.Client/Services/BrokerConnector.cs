using Broker;
using Google.Protobuf.Collections;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using MiniBroker.Grpc.Client.Contracts;

namespace MiniBroker.Grpc.Client.Services;

public class RegistryClientWrapper : IRegistryClientWrapper
{
    public AsyncDuplexStreamingCall<NotificationRequest, MessageStreamProto> OpenEventStream(
        Registry.RegistryClient client)
    {
        return client!.OpenEventStream();
    }
}

public interface IRegistryClientWrapper
{
    AsyncDuplexStreamingCall<NotificationRequest, MessageStreamProto> OpenEventStream(Registry.RegistryClient client);
}

public class BrokerConnector(
    ILogger<BrokerConnector> logger,
    IMessageDispatcherService messageDispatcherService,
    IRegistryClientWrapper registryClientWrapper) : IBrokerConnector, IAsyncDisposable
{
    private const int MaxRetryAttempts = 5;
    private const int BaseDelayMilliseconds = 1000; // 1 second base backoff
    private GrpcChannel? _channel;
    private Registry.RegistryClient? _client;
    private string? _clientId;
    private CancellationTokenSource? _connectionSource;

    private short _retryCounter;

    public async ValueTask DisposeAsync()
    {
        if (_connectionSource != null) await CastAndDispose(_connectionSource);
        if (_channel != null) await CastAndDispose(_channel);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }

    public string GetClientId()
    {
        if (_clientId == null)
            throw new InvalidOperationException("Client ID is not set. Please start listening first.");
        return _clientId;
    }

    public Registry.RegistryClient GetClient()
    {
        if (_client == null) throw new InvalidOperationException("Client is not set. Please start listening first.");
        return _client;
    }

    /// <summary>
    ///     Start Listening to all notifications from the server
    /// </summary>
    /// <param name="address">Server to connect https://localhost:5001</param>
    /// <param name="clientId">Unique id of the user : client1</param>
    /// <param name="cancellationToken">Stop the listener</param>
    /// <returns></returns>
    public async Task StartListeningAsync(string address, string clientId, CancellationToken cancellationToken)
    {
        _connectionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _clientId = clientId;

        _channel = GrpcChannel.ForAddress(address);
        _client = new Registry.RegistryClient(_channel);

        await OpenServerStream(clientId, _connectionSource.Token);

        if (_channel != null)
            await _channel.ShutdownAsync();
        logger.LogInformation("Listener stopped and channel closed.");
    }

    /// <summary>
    ///     Stop listening to all notifications from the server
    /// </summary>
    /// <returns></returns>
    public async Task StopListeningAsync()
    {
        if (_connectionSource != null)
            await _connectionSource.CancelAsync();
        await DisposeAsync();
    }


    
    private async Task OpenServerStream(string clientId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                using var call = registryClientWrapper.OpenEventStream(_client!);
                var subscribedTypes = messageDispatcherService.GetSubscribedTypes();
                var connectionRequest = new NotificationRequest
                    { ClientId = clientId};
                connectionRequest.SubscribedTypes.AddRange(subscribedTypes);
                // Send initial identification message
                await call.RequestStream.WriteAsync(connectionRequest, cancellationToken);
                
                logger.LogInformation("Connected to gRPC server at {Address} as {ClientId}", _channel?.Target,
                    clientId);

                // Reset retry counter on success
                _retryCounter = 0;

                while (await call.ResponseStream.MoveNext(cancellationToken))
                {
                    var response = call.ResponseStream.Current;
                    messageDispatcherService.ProcessMessage(response);
                }

                // If the loop ends naturally (e.g., stream closed), break
                break;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Listening canceled.");
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                logger.LogInformation("gRPC call cancelled by server.");
                break;
            }
            catch (RpcException ex) when (IsTransient(ex.StatusCode))
            {
                if (!await CanRetry(ex, cancellationToken))
                    break; // Stop retrying if CanRetry returns false
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in gRPC listener.");
                break;
            }
    }

    private static bool IsTransient(StatusCode statusCode)
    {
        return statusCode == StatusCode.Unavailable ||
               statusCode == StatusCode.DeadlineExceeded ||
               statusCode == StatusCode.Aborted ||
               statusCode == StatusCode.ResourceExhausted;
    }

    private async Task<bool> CanRetry(RpcException ex, CancellationToken ct)
    {
        _retryCounter++;
        var delay = GetRetryDelay(_retryCounter);
        logger.LogWarning(ex, "gRPC connection failed (attempt {Attempt}). Retrying in {Delay}...", _retryCounter,
            delay);

        if (_retryCounter >= MaxRetryAttempts)
        {
            logger.LogError("Maximum retry attempts reached. Aborting.");
            return false;
        }

        try
        {
            if (!ct.IsCancellationRequested)
                await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return !ct.IsCancellationRequested;
    }

    private TimeSpan GetRetryDelay(short counter)
    {
        var baseDelay = BaseDelayMilliseconds * Math.Pow(2, counter);
        var random = new Random();
        var randomValue = random.NextDouble();
        var jitter = randomValue * 0.3 + 0.85; // ~±15%
        return TimeSpan.FromMilliseconds(baseDelay * jitter);
    }
}