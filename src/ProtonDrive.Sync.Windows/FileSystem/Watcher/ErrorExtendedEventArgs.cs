using System;
using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Provides data for the <see cref='FileSystemExtendedWatcher.Error'/> event.
/// </summary>
public class ErrorExtendedEventArgs : ErrorEventArgs
{
    public ErrorExtendedEventArgs(Exception exception)
        : base(exception)
    {
    }
}
