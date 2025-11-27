using System.Text.Json;
using System.Threading.Channels;
using Broker;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using MiniBroker.Abstraction;
using MiniBroker.Worker.Data.Cache;
using MiniBroker.Worker.Services;

namespace MiniBroker.Worker.Rpc;

public class BrokerService(ILogger<BrokerService> logger, MessageService messageService, IConnectionService connection)
    : Registry.RegistryBase
{
    private readonly int _maxRetry = 5;

    /// <summary>
    ///     Endpoint Send a message to a client
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task<Empty> Send(MessageStreamProto request, ServerCallContext context)
    {
        await ForwardMessage(request);
        return new Empty();
    }

    /// <summary>
    ///     Endpoint Establish connection client-server
    /// </summary>
    /// <param name="requestStream"></param>
    /// <param name="responseStream"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task OpenEventStream(
        IAsyncStreamReader<NotificationRequest> requestStream,
        IServerStreamWriter<MessageStreamProto> responseStream,
        ServerCallContext context)
    {
        var csSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var clientId = string.Empty;

        try
        {
            // Read the first message to get the client ID
            if (await requestStream.MoveNext())
                clientId = await Connect(requestStream, responseStream, context, csSource);

            await SendPendingMessages(clientId, csSource.Token);

            var tcs = new TaskCompletionSource();
            await using (csSource.Token.Register(() => tcs.TrySetResult()))
            {
                await tcs.Task; // Wait until the cancellation token is triggered
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error with client {clientId}: {ex.Message}");
        }
        finally
        {
            // Do not disconnect the client if it's from a reconnection, and the previous stream is cleaned
            if (!string.IsNullOrEmpty(clientId) && context.CancellationToken.IsCancellationRequested)
            {
                logger.LogInformation($"Client {clientId} logout.");
                messageService.Disconnect(clientId);
            }
        }
    }

    private async Task SendPendingMessages(string clientId, CancellationToken ct)
    {
        try
        {
            var channel = messageService.TryGetChannel(clientId);
            if (channel != null)
                while (await channel.Reader.WaitToReadAsync(ct))
                while (channel.Reader.TryRead(out var message))
                {
                    var isSuccess = await ForwardMessage(message, false);
                    if (isSuccess)
                    {
                        messageService.RemoveMessageFromQueue(message);
                        continue;
                    }

                    //handle retry/fail
                    await HandleRetryAsync(message, channel, ct);
                }
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
        }
    }
    
    private async Task HandleRetryAsync(MessageStreamProto message, Channel<MessageStreamProto> channel, CancellationToken ct)
    {
        message.Context.RetryCount++;

        if (message.Context.RetryCount > _maxRetry)
        {
            messageService.TryAddMessageToFailQueue(message);
            messageService.RemoveMessageFromQueue(message);
            logger.LogWarning($"Message to {message.Context.Destinataire} moved to fail queue after {_maxRetry} retries");
        }
        else
        {
            await channel.Writer.WriteAsync(message, ct);
            logger.LogInformation($"Retry {message.Context.RetryCount} for {message.Context.Destinataire}");
        }
    }

    private async Task<bool> ForwardMessage(MessageStreamProto request, bool addToCache = true)
    {
        var clientId = request.Context.Destinataire;
        var clientStream = connection.TryGetClientStreamWriter(clientId);
        if (clientStream == null)
        {
            logger.LogWarning($"Client {clientId} not connected.");

            if (addToCache)
                messageService.TryAddMessageToQueue(request);
            return false;
        }

        try
        {
            var notification = new MessageStreamProto
            {
                Type = request.Type,
                Payload = request.Payload,
                Context = request.Context
            };

            await clientStream.WriteAsync(notification);
            logger.LogInformation($"Sent notification to {clientId}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error sending to client {clientId}: {ex.Message}");
            messageService.Disconnect(clientId);
        }

        return false;
    }

    private async Task SendMessageFromServer<T>(string clientId, T message)
    {
        if (message == null) return;

        var clientStream = connection.TryGetClientStreamWriter(clientId);
        if (clientStream == null)
        {
            logger.LogWarning($"Client {clientId} not connected.");
            return;
        }

        try
        {
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(message);
            var notification = new MessageStreamProto
            {
                Type = message.GetType().FullName,
                Payload = ByteString.CopyFrom(jsonBytes),
                Context = new ContextProto
                {
                    Destinataire = clientId,
                    Source = "MiniBrokerServer",
                    PersistMessage = false // no persistence for system notifications
                }
            };

            await clientStream.WriteAsync(notification);
            logger.LogInformation($"Sent notification to {clientId}: {message}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error sending to client {clientId}: {ex.Message}");
            messageService.Disconnect(clientId);
        }
    }

    private async Task<string> Connect(IAsyncStreamReader<NotificationRequest> requestStream,
        IServerStreamWriter<MessageStreamProto> responseStream, ServerCallContext context,
        CancellationTokenSource csSource)
    {
        var clientId = connection.AddClient(requestStream, responseStream, csSource);
        messageService.Connect(clientId, context);
        logger.LogInformation($"Client {clientId} connected.");
        await SendMessageFromServer(clientId, new SystemNotificationMessage { Message = $"Welcome {clientId}" });
        return clientId;
    }
}