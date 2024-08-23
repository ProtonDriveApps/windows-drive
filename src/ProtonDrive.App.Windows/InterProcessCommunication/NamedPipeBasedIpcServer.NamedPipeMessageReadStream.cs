using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Windows.InterProcessCommunication;

internal sealed partial class NamedPipeBasedIpcServer
{
    /// <summary>
    /// Stream that ends on a complete message
    /// </summary>
    private sealed class NamedPipeMessageReadStream : Stream
    {
        private static readonly Task<int> MessageCompleteTask = Task.FromResult(0);

        private readonly NamedPipeServerStream _pipeServerStream;
        private bool _messageIsComplete;

        public NamedPipeMessageReadStream(NamedPipeServerStream pipeServerStream)
        {
            _pipeServerStream = pipeServerStream;
        }

        public override bool CanRead => _pipeServerStream.CanRead;

        public override bool CanSeek => _pipeServerStream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _pipeServerStream.Length;

        public override long Position
        {
            get => _pipeServerStream.Position;
            set => _pipeServerStream.Position = value;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_messageIsComplete)
            {
                return 0;
            }

            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            var result = _pipeServerStream.Read(buffer, offset, count);
            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            return result;
        }

        public override int Read(Span<byte> buffer)
        {
            if (_messageIsComplete)
            {
                return 0;
            }

            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            var result = _pipeServerStream.Read(buffer);
            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            return result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_messageIsComplete)
            {
                return 0;
            }

            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            var result = await _pipeServerStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            return result;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_messageIsComplete)
            {
                return 0;
            }

            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            var result = await _pipeServerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            return result;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            if (_messageIsComplete)
            {
                return MessageCompleteTask;
            }

            _messageIsComplete |= _pipeServerStream.IsMessageComplete;
            return _pipeServerStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (ReferenceEquals(asyncResult, MessageCompleteTask))
            {
                return 0;
            }

            var result = _pipeServerStream.EndRead(asyncResult);
            _messageIsComplete = _pipeServerStream.IsMessageComplete;
            return result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _pipeServerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _pipeServerStream.Write(buffer, offset, count);
        }
    }
}
