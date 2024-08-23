namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Represents the method that will handle the <see cref='FileSystemExtendedWatcher.Error'/>
/// event of a <see cref='FileSystemExtendedWatcher'/>.
/// </summary>
public delegate void ErrorExtendedEventHandler(object sender, ErrorExtendedEventArgs e);
