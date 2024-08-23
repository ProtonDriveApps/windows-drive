using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.InterProcessCommunication;

namespace ProtonDrive.App.Windows.InterProcessCommunication;

internal sealed partial class NamedPipeBasedIpcServer
{
    /// <summary>
    /// Allows the IPC message handler to send a response message
    /// </summary>
    private readonly struct IpcResponder : IIpcResponder
    {
        private readonly Stream _responseStream;

        public IpcResponder(Stream responseStream)
        {
            _responseStream = responseStream;
        }

        public async Task Respond<T>(T value, CancellationToken cancellationToken)
        {
            await JsonSerializer.SerializeAsync(_responseStream, value, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}
