using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;
using static Vanara.PInvoke.CldApi.CF_OPERATION_PARAMETERS;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class SyncRootCallbackDispatcher : IAsyncDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource? _commonCancellationTokenSource = new();
    private readonly SemaphoreSlim _disposalSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<CF_TRANSFER_KEY, CancellationTokenSource> _cancellationTokenSources = new();
    private readonly ConcurrentDictionary<CF_TRANSFER_KEY, Task> _transferTasks = new();
    private readonly IFileHydrationDemandHandler<long> _fileHydrationDemandHandler;
    private readonly ILogger<SyncRootCallbackDispatcher> _logger;

    public SyncRootCallbackDispatcher(
        IFileHydrationDemandHandler<long> fileHydrationDemandHandler,
        ILogger<SyncRootCallbackDispatcher> logger)
    {
        _fileHydrationDemandHandler = fileHydrationDemandHandler;
        _logger = logger;

        CallbackTable = new[]
        {
            new() { Type = CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_FETCH_DATA, Callback = FetchData },
            new() { Type = CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_CANCEL_FETCH_DATA, Callback = CancelFetchData },
            CF_CALLBACK_REGISTRATION.CF_CALLBACK_REGISTRATION_END,
        };

        // TODO: prevent exceptions in constructor
        CfGetPlatformInfo(out var platformVersion).ThrowExceptionForHR();

        _logger.LogInformation(
            "Platform version: RevisionNumber = {RevisionNumber:x8}, BuildNumber = {BuildNumber:x8}, IntegrationNumber = {IntegrationNumber:x8}",
            platformVersion.RevisionNumber,
            platformVersion.BuildNumber,
            platformVersion.IntegrationNumber);
    }

    public CF_CALLBACK_REGISTRATION[] CallbackTable { get; }

    public async ValueTask DisposeAsync()
    {
        if (await _disposalSemaphore.WaitAsync(WaitTimeout).ConfigureAwait(false))
        {
            try
            {
                if (_commonCancellationTokenSource is not null)
                {
                    _commonCancellationTokenSource.Cancel();
                    _commonCancellationTokenSource.Dispose();
                }
            }
            finally
            {
                _disposalSemaphore.Release();
            }
        }

        await Task.WhenAll(_transferTasks.Values).ConfigureAwait(false);
    }

    [SuppressMessage("Roslynator", "RCS1242:Do not pass non-read-only struct by read-only reference.", Justification = "Imposed by 3rd party library")]
    private static NodeInfo<long> GetLocalFileInfo(in CF_CALLBACK_INFO callbackInfo)
    {
        var filePath = callbackInfo.VolumeDosName + callbackInfo.NormalizedPath;

        using var file = FileSystemFile.Open(
            filePath,
            FileMode.Open,
            FileSystemFileAccess.ReadAttributes,
            FileShare.ReadWrite | FileShare.Delete);

        return file.ToNodeInfo(parentId: default, refresh: false).WithPath(filePath);
    }

    private static long GetActualRequiredLength(long requiredLength, long fileSize)
    {
        // On Windows 10, the required length given by the callback parameters appears to be the file size modulo 2^32.
        // On Windows 11, it's simply the file size.
        // This forces it to be the file size.
        return Math.Max(requiredLength, fileSize);
    }

    private void AbortTransfer(
        CF_CONNECTION_KEY connectionKey,
        CF_TRANSFER_KEY transferKey,
        CF_REQUEST_KEY requestKey,
        Exception? dataTransferException,
        long requiredFileOffset,
        long requiredLength,
        long localFileId)
    {
        _logger.LogInformation(
            "Aborting TRANSFER_DATA for TransferKey={TransferKey}, RequestKey={RequestKey}, RequiredFileOffset={RequiredFileOffset}, RequiredLength={RequiredLength}",
            transferKey.GetHashCode(),
            requestKey.GetHashCode(),
            requiredFileOffset,
            requiredLength);

        if (dataTransferException is not null && ExceptionMapping.TryMapException(dataTransferException, localFileId, out var mappedDataTransferException))
        {
            dataTransferException = mappedDataTransferException;
        }

        var operation = new CF_OPERATION_INFO
        {
            StructSize = (uint)Marshal.SizeOf<CF_OPERATION_INFO>(),
            Type = CF_OPERATION_TYPE.CF_OPERATION_TYPE_TRANSFER_DATA,
            ConnectionKey = connectionKey,
            TransferKey = transferKey,
            RequestKey = requestKey,
        };

        var parameters = new CF_OPERATION_PARAMETERS
        {
            ParamSize = CF_SIZE_OF_OP_PARAM<TRANSFERDATA>(),
            TransferData = new TRANSFERDATA
            {
                CompletionStatus = NTStatus.STATUS_UNSUCCESSFUL,
                Length = requiredLength,
                Offset = requiredFileOffset,
            },
        };

        try
        {
            CfExecute(operation, ref parameters).ThrowExceptionForHR();
        }
        catch (OperationCanceledException)
        {
            // Data transfer was already cancelled
        }
        catch (Exception abortionException)
        {
            if (ExceptionMapping.TryMapException(abortionException, localFileId, out var mappedAbortionException))
            {
                abortionException = mappedAbortionException;
            }

            throw dataTransferException is not null ? new AggregateException(dataTransferException, abortionException) : abortionException;
        }
    }

    [SuppressMessage("Roslynator", "RCS1242:Do not pass non-read-only struct by read-only reference.", Justification = "Imposed by 3rd party library")]
    private void CancelFetchData(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        var transferKey = callbackInfo.TransferKey;
        var requestKey = callbackInfo.RequestKey;
        var requiredFileOffset = callbackParameters.FetchData.RequiredFileOffset;
        var requiredLength = GetActualRequiredLength(callbackParameters.FetchData.RequiredLength, callbackInfo.FileSize);

        _logger.LogInformation(
            "CANCEL_FETCH_DATA received for TransferKey={TransferKey}, RequestKey={RequestKey}, RequiredFileOffset={RequiredFileOffset}, RequiredLength={RequiredLength}",
            transferKey.GetHashCode(),
            requestKey.GetHashCode(),
            requiredFileOffset,
            requiredLength);

        if (!_cancellationTokenSources.TryRemove(callbackInfo.TransferKey, out var cancellationTokenSource))
        {
            return;
        }

        if (!_disposalSemaphore.Wait(WaitTimeout))
        {
            return;
        }

        try
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        finally
        {
            _disposalSemaphore.Release();
        }
    }

    [SuppressMessage("Roslynator", "RCS1242:Do not pass non-read-only struct by read-only reference.", Justification = "Imposed by 3rd party library")]
    private void FetchData(in CF_CALLBACK_INFO callbackInfo, in CF_CALLBACK_PARAMETERS callbackParameters)
    {
        var processInfo = (callbackInfo.ProcessInfo != IntPtr.Zero) ? (CF_PROCESS_INFO?)Marshal.PtrToStructure<CF_PROCESS_INFO>(callbackInfo.ProcessInfo) : null;
        var processName = _logger.GetSensitiveValueForLogging(Path.GetFileName(processInfo?.ImagePath) ?? "<Unknown>");

        var connectionKey = callbackInfo.ConnectionKey;
        var transferKey = callbackInfo.TransferKey;
        var requestKey = callbackInfo.RequestKey;
        var requiredFileOffset = callbackParameters.FetchData.RequiredFileOffset;
        var requiredLength = GetActualRequiredLength(callbackParameters.FetchData.RequiredLength, callbackInfo.FileSize);

        _logger.LogInformation(
            "FETCH_DATA received for TransferKey={TransferKey}, RequestKey={RequestKey}, RequiredFileOffset={RequiredFileOffset}, RequiredLength={RequiredLength}, ProcessName=\"{ProcessName}\"",
            transferKey.GetHashCode(),
            requestKey.GetHashCode(),
            requiredFileOffset,
            requiredLength,
            processName);

        if (!_disposalSemaphore.Wait(WaitTimeout))
        {
            AbortTransfer(connectionKey, transferKey, requestKey, default, requiredFileOffset, requiredLength, callbackInfo.FileId);
            return;
        }

        CancellationTokenSource cancellationTokenSource;

        try
        {
            if (_transferTasks.TryGetValue(callbackInfo.TransferKey, out var existingTask))
            {
                // CloudFiles is reusing the same transfer key for a new transfer, wait for the previous task to reach completion first.
                existingTask.Wait(WaitTimeout);
            }

            if (requiredFileOffset > 0)
            {
                _logger.LogInformation(
                    "Sending RESTART_HYDRATION for TransferKey={TransferKey}, RequestKey={RequestKey}",
                    transferKey.GetHashCode(),
                    requestKey.GetHashCode());

                // If the offset is not 0, we can assume that this is trying to resume a previously failed hydration.
                // At the moment, we do not support that, so we request a hydration restart.
                RestartHydration(connectionKey, transferKey, requestKey, requiredFileOffset, requiredLength, callbackInfo.FileId);
                return;
            }

            if (_commonCancellationTokenSource?.IsCancellationRequested != false)
            {
                return;
            }

            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_commonCancellationTokenSource.Token);
            _cancellationTokenSources.AddOrUpdate(callbackInfo.TransferKey, cancellationTokenSource, (_, _) => cancellationTokenSource);
        }
        finally
        {
            _disposalSemaphore.Release();
        }

        NodeInfo<long> localFileInfo;
        try
        {
            localFileInfo = GetLocalFileInfo(callbackInfo);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogError("Failed to access placeholder file: {ExceptionType} {HResult}", ex.GetType().Name, ex.HResult);

            AbortTransfer(connectionKey, transferKey, requestKey, ex, requiredFileOffset, requiredLength, default);

            if (_cancellationTokenSources.TryRemove(transferKey, out var cts))
            {
                cts.Dispose();
            }

            return;
        }

        var wrappedTransferTask = new Task<Task>(
            () => HydrateFileAsync(localFileInfo, connectionKey, transferKey, requestKey, requiredFileOffset, requiredLength, cancellationTokenSource.Token));

        var transferTask = wrappedTransferTask.Unwrap();

        _transferTasks.AddOrUpdate(transferKey, transferTask, (_, _) => transferTask);

        // This will call the async method, without awaiting it, just to start the underlying task
        wrappedTransferTask.RunSynchronously();
    }

    private async Task HydrateFileAsync(
        NodeInfo<long> fileInfo,
        CF_CONNECTION_KEY connectionKey,
        CF_TRANSFER_KEY transferKey,
        CF_REQUEST_KEY requestKey,
        long requiredFileOffset,
        long requiredLength,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataTransferStream = new CloudFilesDataTransferStream(
                fileInfo.Id,
                connectionKey,
                transferKey,
                requestKey,
                requiredFileOffset,
                requiredLength,
                _logger);

            using var hydrationProcess = new FileHydrationProcess<long>(fileInfo, dataTransferStream, UpdateFileSize);
            {
                await _fileHydrationDemandHandler.HandleAsync(hydrationProcess, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Hydrating file with Id={Id} was cancelled", fileInfo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError("Hydrating file with Id={Id} failed: {ErrorMessage}", fileInfo.Id, ex.CombinedMessage());

            AbortTransfer(connectionKey, transferKey, requestKey, ex, requiredFileOffset, requiredLength, fileInfo.Id);
        }
        finally
        {
            if (_cancellationTokenSources.TryRemove(transferKey, out var cancellationTokenSource))
            {
                cancellationTokenSource.Dispose();
            }

            _transferTasks.TryRemove(transferKey, out _);
        }

        NodeInfo<long> UpdateFileSize(long newSize)
        {
            using var file = fileInfo.OpenAsFile(FileMode.Open, FileSystemFileAccess.WriteAttributes, FileShare.ReadWrite | FileShare.Delete);

            file.ThrowIfMetadataMismatch(fileInfo);

            file.UpdatePlaceholder(new CF_FS_METADATA { FileSize = newSize }, CF_UPDATE_FLAGS.CF_UPDATE_FLAG_NONE);

            var operation = new CF_OPERATION_INFO
            {
                StructSize = (uint)Marshal.SizeOf<CF_OPERATION_INFO>(),
                Type = CF_OPERATION_TYPE.CF_OPERATION_TYPE_ACK_DATA,
                ConnectionKey = connectionKey,
                TransferKey = transferKey,
            };

            var parameters = new CF_OPERATION_PARAMETERS
            {
                ParamSize = CF_SIZE_OF_OP_PARAM<ACKDATA>(),
                AckData = new ACKDATA
                {
                    Flags = CF_OPERATION_ACK_DATA_FLAGS.CF_OPERATION_ACK_DATA_FLAG_NONE,
                    CompletionStatus = NTStatus.STATUS_SUCCESS,
                    Offset = requiredFileOffset,
                    Length = newSize - requiredFileOffset,
                },
            };

            CfExecute(operation, ref parameters).ThrowExceptionForHR();

            parameters = new CF_OPERATION_PARAMETERS
            {
                ParamSize = CF_SIZE_OF_OP_PARAM<ACKDATA>(),
                AckData = new ACKDATA
                {
                    Flags = CF_OPERATION_ACK_DATA_FLAGS.CF_OPERATION_ACK_DATA_FLAG_NONE,
                    CompletionStatus = NTStatus.STATUS_SUCCESS,
                    Offset = requiredFileOffset,
                    Length = -1,
                },
            };

            CfExecute(operation, ref parameters).ThrowExceptionForHR();

            return file.ToNodeInfo(parentId: default, refresh: true);
        }
    }

    private void RestartHydration(
        CF_CONNECTION_KEY connectionKey,
        CF_TRANSFER_KEY transferKey,
        CF_REQUEST_KEY requestKey,
        long requiredFileOffset,
        long requiredLength,
        long localFileId)
    {
        var operation = new CF_OPERATION_INFO
        {
            StructSize = (uint)Marshal.SizeOf<CF_OPERATION_INFO>(),
            Type = CF_OPERATION_TYPE.CF_OPERATION_TYPE_RESTART_HYDRATION,
            ConnectionKey = connectionKey,
            TransferKey = transferKey,
            RequestKey = requestKey,
        };

        var parameters = new CF_OPERATION_PARAMETERS
        {
            ParamSize = CF_SIZE_OF_OP_PARAM<RESTARTHYDRATION>(),
            RestartHydration = new RESTARTHYDRATION { Flags = CF_OPERATION_RESTART_HYDRATION_FLAGS.CF_OPERATION_RESTART_HYDRATION_FLAG_NONE },
        };

        try
        {
            CfExecute(operation, ref parameters).ThrowExceptionForHR();
        }
        catch (Exception ex)
        {
            AbortTransfer(connectionKey, transferKey, requestKey, ex, requiredFileOffset, requiredLength, localFileId);
        }
    }
}
