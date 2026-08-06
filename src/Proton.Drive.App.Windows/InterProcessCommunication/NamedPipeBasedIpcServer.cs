using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.InterProcessCommunication;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.InterProcessCommunication;

internal sealed partial class NamedPipeBasedIpcServer : IStartableService, IStoppableService
{
    public const string PipeName = "ProtonDrive";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _name;
    private readonly Lazy<Dictionary<string, IIpcMessageHandler>> _messageHandlers;
    private readonly ILogger<NamedPipeBasedIpcServer> _logger;

    private readonly CancellationTokenSource _pipeCancellationTokenSource = new();
    private Task? _listeningTask;

    public NamedPipeBasedIpcServer(string name, Lazy<IEnumerable<IIpcMessageHandler>> messageHandlers, ILogger<NamedPipeBasedIpcServer> logger)
    {
        _name = name;
        _messageHandlers = new Lazy<Dictionary<string, IIpcMessageHandler>>(() => messageHandlers.Value.ToDictionary(x => x.MessageType));
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listeningTask is not null)
        {
            throw new InvalidOperationException();
        }

        _listeningTask = RunAsync(_pipeCancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listeningTask is null)
        {
            return;
        }

        await _pipeCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        try
        {
            await _listeningTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected exception occurred while stopping the IPC server.");
        }

        _listeningTask = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var namedPipeStream = new NamedPipeServerStream(
                        _name,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    try
                    {
                        await HandleIncomingConnectionAsync(namedPipeStream, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        await namedPipeStream.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        "An unexpected exception occurred while running the IPC server: {ExceptionType} {ErrorCode}",
                        ex.GetType().Name,
                        ex.GetRelevantFormattedErrorCode());

                    // Delay to avoid excessive logging and CPU usage in case it fails every time
                    await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
    }

    private async Task HandleIncomingConnectionAsync(NamedPipeServerStream serverStream, CancellationToken cancellationToken)
    {
        await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        ProcessMessageAsync(serverStream, cancellationToken).Forget();
    }

    private async Task ProcessMessageAsync(NamedPipeServerStream serverStream, CancellationToken cancellationToken)
    {
        try
        {
            // Yield immediately so that the server can wait for another connection as soon as possible
            await Task.Yield();

            IpcMessage? message;

            var messageReadStream = new NamedPipeMessageReadStream(serverStream);

            await using (messageReadStream.ConfigureAwait(false))
            {
                try
                {
                    message = await JsonSerializer.DeserializeAsync<IpcMessage>(messageReadStream, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("IPC: Message deserialization failed: {ErrorMessage}", ex.Message);
                    return;
                }
            }

            _logger.LogDebug("IPC: Received message of type {Type}, Parameters=\"{Parameters}\"", message?.Type, message?.Parameters);

            if (message?.Type is null)
            {
                _logger.LogWarning("IPC: Received message has no type specified");
                return;
            }

            if (!_messageHandlers.Value.TryGetValue(message.Type, out var messageHandler))
            {
                _logger.LogWarning("IPC: Received message of type {Type} has no dispatcher", message.Type);
                return;
            }

            try
            {
                await messageHandler.HandleAsync(message.Parameters, new IpcResponder(serverStream), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "IPC: Exception occurred on handler for message type {Type}", message.Type);
            }
        }
        finally
        {
            await serverStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
