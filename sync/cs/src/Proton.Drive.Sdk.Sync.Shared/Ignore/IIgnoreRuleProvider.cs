namespace Proton.Drive.Sdk.Sync.Shared.Ignore;

/// <summary>
/// Provides user defined exclusion rules for a sync root.
/// </summary>
public interface IIgnoreRuleProvider
{
    /// <summary>
    /// Whether user defined ignore rules are honoured at all. When false, callers should skip
    /// building the relative path of an item, which is not free.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Tests an item against the ignore rules of its sync root.
    /// </summary>
    /// <param name="rootId">The sync root (mapping) identity.</param>
    /// <param name="rootLocalPath">The absolute path of the sync root on the local file system.</param>
    /// <param name="relativePath">The path of the item relative to the sync root.</param>
    /// <param name="isDirectory">Whether the item is a directory.</param>
    bool IsIgnored(int rootId, string rootLocalPath, string relativePath, bool isDirectory);

    /// <summary>
    /// Drops all cached ignore files, so that they are read again on the next request.
    /// </summary>
    void Invalidate();
}
