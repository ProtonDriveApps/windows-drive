using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.FileSystem.Enumeration;

internal class FileSystemEnumerable : IEnumerable<FileSystemEntry>
{
    private SafeFileHandle? _directoryHandle;
    private readonly bool _ownsHandle;
    private readonly string? _fileName;
    private readonly EnumerationOptions? _options;

    public FileSystemEnumerable(SafeFileHandle directoryHandle, bool ownsHandle, string? fileName = null, EnumerationOptions? options = null)
    {
        _directoryHandle = directoryHandle;
        _ownsHandle = ownsHandle;
        _fileName = fileName;
        _options = options;
    }

    public IEnumerator<FileSystemEntry> GetEnumerator()
    {
        var handle = Interlocked.Exchange(ref _directoryHandle, null);
        if (handle == null)
        {
            throw new InvalidOperationException("Multiple enumerations is not supported");
        }

        try
        {
            return new FileSystemEnumerator(handle, _ownsHandle, _fileName, _options);
        }
        catch
        {
            if (_ownsHandle)
            {
                handle.Close();
            }

            throw;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
