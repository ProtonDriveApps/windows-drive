using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.FileSystem;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;
using static Vanara.PInvoke.CldApi.CF_OPERATION_PARAMETERS;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class CloudFilesDataTransferStream : Stream
{
    private const int CloudFilesAlignment = 4_096;

    private static readonly Action<int> NoValidationAction = _ => { };

    private readonly long _id;
    private readonly ILogger _logger;

    private readonly CF_OPERATION_INFO _operation;

    private readonly BufferedLengthAligner<byte> _writeAligner;

    private readonly Action<int> _validationAction;
    private readonly ReadOnlySpanAction<byte, CloudFilesDataTransferStream> _writeChunkAction;
    private readonly ReadOnlySpanAction<byte, CloudFilesDataTransferStream> _writeFinalChunkAction;

    private bool _isDisposed;
    private long _position;
    private long _transferPosition;
    private long _length;

    public CloudFilesDataTransferStream(
        long id,
        CF_CONNECTION_KEY connectionKey,
        CF_TRANSFER_KEY transferKey,
        CF_REQUEST_KEY requestKey,
        long requiredOffset,
        long requiredLength,
        ILogger logger)
    {
        _operation = new CF_OPERATION_INFO
        {
            StructSize = (uint)Marshal.SizeOf<CF_OPERATION_INFO>(),
            Type = CF_OPERATION_TYPE.CF_OPERATION_TYPE_TRANSFER_DATA,
            ConnectionKey = connectionKey,
            TransferKey = transferKey,
            RequestKey = requestKey,
        };

        _id = id;
        _position = requiredOffset;
        _transferPosition = _position;
        _logger = logger;
        _writeAligner = new BufferedLengthAligner<byte>(CloudFilesAlignment);

        _length = requiredOffset + requiredLength;
        _validationAction = ValidateChunk;
        _writeChunkAction = (span, @this) => @this.WriteChunk(span);
        _writeFinalChunkAction = (span, @this) => @this.WriteFinalChunk(span);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // Nothing to flush
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value)
    {
        if (value == _position)
        {
            FlushCarryOver();
        }

        _length = value;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var newPositionAfterWrite = _position + buffer.Length;
        var exceedsFileSize = newPositionAfterWrite > _length;
        if (exceedsFileSize)
        {
            throw new FileSystemClientException("Attempted to write too many bytes", FileSystemErrorCode.Unknown);
        }

        _writeAligner.InvokeWithLengthAlignment(_writeChunkAction, this, buffer);

        if (newPositionAfterWrite == _length)
        {
            _writeAligner.InvokeOnCarryOver(_writeChunkAction, this);
        }

        _position = newPositionAfterWrite;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;

            FlushCarryOver();
        }

        base.Dispose(disposing);
    }

    private unsafe void TransferChunk(ReadOnlySpan<byte> chunk)
    {
        fixed (byte* chunkPointer = &MemoryMarshal.GetReference(chunk))
        {
            var parameters = new CF_OPERATION_PARAMETERS
            {
                ParamSize = CF_SIZE_OF_OP_PARAM<TRANSFERDATA>(),
                TransferData = new TRANSFERDATA
                {
                    CompletionStatus = NTStatus.STATUS_SUCCESS,
                    Buffer = new IntPtr(chunkPointer),
                    Offset = _transferPosition,
                    Length = chunk.Length,
                },
            };

            try
            {
                _logger.LogDebug(
                    "TRANSFER_DATA for TransferKey={TransferKey}, RequestKey={RequestKey}, Offset={_position}, Length={Count}",
                    _operation.TransferKey.GetHashCode(),
                    _operation.RequestKey.GetHashCode(),
                    _transferPosition,
                    chunk.Length);

                CfExecute(_operation, ref parameters).ThrowExceptionForHR();
            }
            catch (Exception e) when (ExceptionMapping.TryMapException(e, _id, out var mappedException))
            {
                throw mappedException;
            }
        }
    }

    private void ValidateChunk(int length)
    {
        var operation = _operation;
        operation.Type = CF_OPERATION_TYPE.CF_OPERATION_TYPE_ACK_DATA;

        var parameters = new CF_OPERATION_PARAMETERS
        {
            ParamSize = CF_SIZE_OF_OP_PARAM<ACKDATA>(),
            AckData = new ACKDATA
            {
                Flags = CF_OPERATION_ACK_DATA_FLAGS.CF_OPERATION_ACK_DATA_FLAG_NONE,
                CompletionStatus = NTStatus.STATUS_SUCCESS,
                Offset = _transferPosition,
                Length = length,
            },
        };

        CfExecute(operation, ref parameters).ThrowIfFailed();
    }

    private void FlushCarryOver()
    {
        try
        {
            _writeAligner.InvokeOnCarryOver(_writeFinalChunkAction, this);
        }
        catch
        {
            // Clear the carry-over to prevent further exceptions on disposing
            _writeAligner.Reset();
            throw;
        }
    }

    private void WriteFinalChunk(ReadOnlySpan<byte> chunk)
    {
        var remainingNumberOfBytesToWrite = (int)(_length - _transferPosition);
        if (remainingNumberOfBytesToWrite <= 0)
        {
            return;
        }

        if (chunk.Length == remainingNumberOfBytesToWrite)
        {
            WriteChunk(chunk);
            return;
        }

        WriteChunkWithFinalPadding(chunk, remainingNumberOfBytesToWrite);
    }

    private void WriteChunkWithFinalPadding(ReadOnlySpan<byte> chunk, int remainingNumberOfBytesToWrite)
    {
        const int maxFinalBufferLength = (16_384 / CloudFilesAlignment) * CloudFilesAlignment;

        Span<byte> finalBuffer = stackalloc byte[Math.Min(remainingNumberOfBytesToWrite, maxFinalBufferLength)];
        chunk.CopyTo(finalBuffer);

        do
        {
            var spanToWrite = finalBuffer[..Math.Min(remainingNumberOfBytesToWrite, maxFinalBufferLength)];

            // We must not validate the last chunk nor the padding, as those need to be validated after the placeholder has been resized.
            WriteChunkWithoutValidation(spanToWrite);

            remainingNumberOfBytesToWrite -= spanToWrite.Length;
        }
        while (remainingNumberOfBytesToWrite > 0);
    }

    private void WriteChunk(ReadOnlySpan<byte> chunk)
    {
        WriteChunk(chunk, _validationAction);
    }

    private void WriteChunkWithoutValidation(ReadOnlySpan<byte> chunk)
    {
        WriteChunk(chunk, NoValidationAction);
    }

    private void WriteChunk(ReadOnlySpan<byte> chunk, Action<int> validationAction)
    {
        TransferChunk(chunk);
        validationAction.Invoke(chunk.Length);
        _transferPosition += chunk.Length;
    }
}
