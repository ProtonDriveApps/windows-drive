using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Windows.InterProcessCommunication;

public sealed class NamedPipeBasedIpcClient : IDisposable, IAsyncDisposable
{
    // ReSharper disable once IdentifierTypo
    // ReSharper disable once InconsistentNaming
#pragma warning disable SA1310
    private const int NMPWAIT_NOWAIT = 1;
#pragma warning restore SA1310

    private const int ReadBufferSize = 128;
    private const int ConnectRetryIntervalMilliseconds = 20;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Stream _stream;

    public NamedPipeBasedIpcClient(Stream stream)
    {
        _stream = stream;
    }

    public static async Task<NamedPipeBasedIpcClient> ConnectAsync(string name, TimeSpan timeoutDelay, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeoutDelay);

        var clientStream = new NamedPipeClientStream(
            ".",
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        try
        {
            while (true)
            {
                try
                {
                    await clientStream.ConnectAsync(NMPWAIT_NOWAIT, cancellationToken).ConfigureAwait(false);

                    return new NamedPipeBasedIpcClient(clientStream);
                }
                catch (TimeoutException) when (!timeoutTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(ConnectRetryIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch
        {
            await clientStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask WriteAsync(string messageType, CancellationToken cancellationToken)
    {
        return WriteAsync(messageType, parameters: null, cancellationToken);
    }

    public ValueTask WriteAsync(string messageType, string? parameters, CancellationToken cancellationToken)
    {
        var message = new IpcMessage { Type = messageType, Parameters = JsonSerializer.SerializeToNode(parameters) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions);

        return _stream.WriteAsync(bytes, cancellationToken);
    }

    public async ValueTask<T?> ReadAsync<T>(CancellationToken cancellationToken)
    {
        var bytes = new byte[ReadBufferSize];
        var numberOfBytesRead = await _stream.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);

        return numberOfBytesRead == 0
            ? default
            : JsonSerializer.Deserialize<T>(bytes.AsSpan()[..numberOfBytesRead]);
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
