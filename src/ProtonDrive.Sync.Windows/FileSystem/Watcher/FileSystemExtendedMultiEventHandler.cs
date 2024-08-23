using System.Collections.Generic;

namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Represents the method that will handle the <see cref='FileSystemExtendedWatcher.Changed'/>,
/// <see cref='FileSystemExtendedWatcher.Created'/>, or
/// <see cref='FileSystemExtendedWatcher.Deleted'/> event of
/// a <see cref='FileSystemExtendedWatcher'/> class.
/// </summary>
public delegate void FileSystemExtendedMultiEventHandler(object sender, IReadOnlyCollection<RenamedExtendedEventArgs> e);
