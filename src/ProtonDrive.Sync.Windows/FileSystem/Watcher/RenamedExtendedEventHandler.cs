namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Represents the method that will handle the <see cref='FileSystemExtendedWatcher.Renamed'/>
/// event of a <see cref='FileSystemExtendedWatcher'/> class.
/// </summary>
public delegate void RenamedExtendedEventHandler(object sender, RenamedExtendedEventArgs e);
