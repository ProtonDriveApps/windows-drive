using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ProtonDrive.Sync.Windows.FileSystem.Internal;
using ProtonDrive.Sync.Windows.Interop;

namespace ProtonDrive.Sync.Windows.FileSystem.Enumeration;

internal unsafe class FileSystemEnumerator : CriticalFinalizerObject, IEnumerator<FileSystemEntry>
{
    private const int StandardBufferSize = 4096;

    // We need to have enough room for at least a single entry. The filename alone can be 255 * 2 = 510 bytes,
    // we'll ensure we have a reasonable buffer for all of the other metadata as well.
    private const int MinimumBufferSize = 1024;

    private readonly SafeFileHandle _directoryHandle;
    private readonly bool _ownsHandle;
    private readonly string? _fileName;
    private readonly EnumerationOptions _options;

    private readonly object? _lock = new object();

    private NtDll.FILE_ID_BOTH_DIR_INFORMATION* _entry;
    private bool _firstEntryFound;
    private bool _lastEntryFound;

    private IntPtr _buffer;
    private int _bufferLength;
    private FileSystemEntry? _current;

    internal FileSystemEnumerator(SafeFileHandle directoryHandle, bool ownsHandle, string? fileName, EnumerationOptions? options = null)
    {
        _directoryHandle = directoryHandle;
        _ownsHandle = ownsHandle;
        _fileName = fileName;
        _options = options ?? new EnumerationOptions();

        Init();
    }

    public FileSystemEntry Current
    {
        get => _current ?? throw new ArgumentNullException(nameof(Current));
        private set => _current = value;
    }

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        if (_lastEntryFound)
        {
            return false;
        }

        lock (_lock!)
        {
            if (_lastEntryFound)
            {
                return false;
            }

            var entry = new FileSystemEntry();

            while (true)
            {
                FindNextEntry();

                if (_lastEntryFound)
                {
                    return false;
                }

                entry.Initialize(_entry);

                if (ShouldIncludeEntry(entry))
                {
                    Current = entry;
                    return true;
                }
            }
        }
    }

    public void Reset()
    {
        throw new NotSupportedException();
    }

    private bool ShouldIncludeEntry(FileSystemEntry entry)
    {
        // Attributes to skip
        if ((entry.Attributes & _options.AttributesToSkip) != 0)
        {
            return false;
        }

        // Special directories are named "." and ".."
        if ((entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory &&
            !_options.ReturnSpecialDirectories &&
            entry.Name.Length <= 2 && entry.Name[0] == '.' &&
            (entry.Name.Length != 2 || entry.Name[1] == '.'))
        {
            return false;
        }

        return true;
    }

    private void Init()
    {
        var requestedBufferSize = _options.BufferSize;
        _bufferLength = requestedBufferSize <= 0
            ? StandardBufferSize
            : Math.Max(MinimumBufferSize, requestedBufferSize);

        // This FILE_ID_BOTH_DIR_INFORMATION structure must be aligned on a LONGLONG (8-byte) boundary.
        // If a buffer contains two or more of these structures, the NextEntryOffset value in each
        // entry, except the last, falls on an 8-byte boundary.
        _buffer = Marshal.AllocHGlobal(_bufferLength);
    }

    private void FindNextEntry()
    {
        _entry = NtDll.FILE_ID_BOTH_DIR_INFORMATION.NextInfo(_entry);
        if (_entry != null)
        {
            return;
        }

        if (GetData())
        {
            _entry = (NtDll.FILE_ID_BOTH_DIR_INFORMATION*)_buffer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetData()
    {
        var fileName = !_firstEntryFound && _fileName != null ? new UNICODE_STRING(_fileName) : null;

        var status =
            NtDll.NtQueryDirectoryFile(
                FileHandle: _directoryHandle,
                Event: IntPtr.Zero,
                ApcRoutine: IntPtr.Zero,
                ApcContext: IntPtr.Zero,
                IoStatusBlock: out NtDll.IO_STATUS_BLOCK statusBlock,
                FileInformation: _buffer.ToPointer(),
                Length: _bufferLength,
                FileInformationClass: NtDll.FILE_INFORMATION_CLASS.FileIdBothDirectoryInformation,
                ReturnSingleEntry: false,
                FileName: fileName,
                RestartScan: !_firstEntryFound);

        switch (status)
        {
            case NtDll.NTSTATUS.STATUS_SUCCESS:
                Debug.Assert(statusBlock.Information.ToInt64() != 0, "statusBlock.Information.ToInt64() != 0");
                _firstEntryFound = true;
                return true;

            case NtDll.NTSTATUS.STATUS_NO_MORE_FILES:
                DirectoryFinished();
                return false;

            // FILE_NOT_FOUND can occur when there are NO files in a volume root (usually there are hidden system files).
            case NtDll.NTSTATUS.STATUS_FILE_NOT_FOUND:
                DirectoryFinished();
                return false;

            default:
                DirectoryFinished();
                var error = (int)NtDll.RtlNtStatusToDosError(status);
                throw Win32Marshal.GetExceptionForWin32Error(error);
        }
    }

    private void InternalDispose(bool disposing)
    {
        // It is possible to fail to allocate the lock, but the finalizer will still run
        if (_lock != null)
        {
            lock (_lock)
            {
                _lastEntryFound = true;

                if (_buffer != default)
                {
                    Marshal.FreeHGlobal(_buffer);
                    _buffer = default;
                }

                if (_ownsHandle)
                {
                    _directoryHandle.Dispose();
                }
            }
        }

        Dispose(disposing);
    }

    private void DirectoryFinished()
    {
        _entry = default;
        _lastEntryFound = true;
    }

    public void Dispose()
    {
        InternalDispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    ~FileSystemEnumerator()
    {
        InternalDispose(disposing: false);
    }
}
