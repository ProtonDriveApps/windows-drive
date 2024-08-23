using System;
using System.IO;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Shared.FileSystem;

public sealed record EventLogEntry<TId>(EventLogChangeType ChangeType)
{
    /// <summary>
    /// The type of change represented by the event log entry.
    /// </summary>
    public EventLogChangeType ChangeType { get; init; } = ChangeType;

    /// <summary>
    /// The file or folder name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The file system object path relative to synchronization root folder.
    /// </summary>
    /// <remarks>
    /// Used only for logging purposes, not used in log-based update detection.
    /// </remarks>
    public string? Path { get; init; }

    /// <summary>
    /// The old file system object path relative to synchronization root folder
    /// in case of rename or move.
    /// </summary>
    /// <remarks>
    /// Used only for logging purposes, not used in log-based update detection.
    /// </remarks>
    public string? OldPath { get; init; }

    /// <summary>
    /// The file system object identity value.
    /// </summary>
    public TId? Id { get; init; }

    /// <summary>
    /// The file system object parent identity value.
    /// </summary>
    public TId? ParentId { get; init; }

    /// <summary>
    /// The Proton Drive file revision identity value.
    /// </summary>
    public string? RevisionId { get; init; }

    /// <summary>
    /// File system object attributes.
    /// </summary>
    public FileAttributes Attributes { get; init; }

    /// <summary>
    /// The UTC date and time of the last change to the file content.
    /// Default value indicates it is unknown.
    /// </summary>
    /// <remarks>
    /// For local folders it's date and time of the last change to the folder content.
    /// For Proton Drive folders it's date and time of the last change to the folder metadata.
    /// The value precision depends on the specific file system.
    /// </remarks>
    public DateTime LastWriteTimeUtc { get; init; }

    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    /// <remarks>
    /// For remote files it can be null if not known. For folders it's null.
    /// </remarks>
    public long? Size { get; init; }

    /// <summary>
    /// The space occupied on storage by the file, in bytes.
    /// </summary>
    /// <remarks>
    /// For local files it's null, we do not tract it. For folders it's null.
    /// It's provided for remote files only.
    /// </remarks>
    public long? SizeOnStorage { get; init; }

    /// <summary>
    /// A value that describes whether the item is a placeholder and if so, what state it is in.
    /// </summary>
    public PlaceholderState PlaceholderState { get; init; }
}
