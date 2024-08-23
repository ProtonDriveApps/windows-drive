namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IRootDirectory<out TId>
{
    TId Id { get; }
    string Path { get; }
}
