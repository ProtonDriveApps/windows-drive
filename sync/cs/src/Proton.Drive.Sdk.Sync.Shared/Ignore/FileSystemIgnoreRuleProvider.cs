using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Proton.Drive.Sdk.Sync.Shared.Ignore;

/// <summary>
/// Reads user defined exclusion rules from the local file system.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds of ignore files are recognised, both in any directory of the sync root: a
/// <c>.protonignore</c> and, when enabled, a <c>.gitignore</c>. Each applies to its own directory
/// and everything below it, exactly as Git does it, so a sync root holding many repositories can
/// keep one <c>.protonignore</c> per repository.
/// </para>
/// <para>
/// Precedence, from weakest to strongest: shallower <c>.gitignore</c>, deeper <c>.gitignore</c>,
/// shallower <c>.protonignore</c>, deeper <c>.protonignore</c>. Any <c>.protonignore</c> outranks
/// every <c>.gitignore</c>, so that a negated pattern there can bring back something a repository
/// <c>.gitignore</c> excludes.
/// </para>
/// <para>
/// Ignore files are cached and re-read when their size or last write time changes. To keep the cost
/// of a full enumeration low, an unchanged file is stat-ed at most once per
/// <see cref="RevalidationInterval"/>.
/// </para>
/// </remarks>
public sealed class FileSystemIgnoreRuleProvider : IIgnoreRuleProvider
{
    public const string ProtonIgnoreFileName = ".protonignore";
    public const string GitIgnoreFileName = ".gitignore";

    private const long MaxIgnoreFileSizeInBytes = 1024 * 1024;

    private static readonly TimeSpan RevalidationInterval = TimeSpan.FromSeconds(2);

    private readonly ILogger<FileSystemIgnoreRuleProvider> _logger;
    private readonly bool _honoursGitIgnoreFiles;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemIgnoreRuleProvider(ILogger<FileSystemIgnoreRuleProvider> logger, bool honoursGitIgnoreFiles = true)
    {
        _logger = logger;
        _honoursGitIgnoreFiles = honoursGitIgnoreFiles;
    }

    public bool IsEnabled => true;

    public bool IsIgnored(int rootId, string rootLocalPath, string relativePath, bool isDirectory)
    {
        if (string.IsNullOrEmpty(rootLocalPath) || string.IsNullOrEmpty(relativePath))
        {
            return false;
        }

        var normalizedPath = relativePath.Replace('\\', '/').Trim('/');

        if (normalizedPath.Length == 0)
        {
            return false;
        }

        var segments = normalizedPath.Split('/');

        // An item is excluded if it matches itself or if any of its ancestor directories is excluded.
        // As in Git, a negated pattern cannot bring back an item from an excluded directory.
        for (var depth = 0; depth < segments.Length; depth++)
        {
            var path = depth == segments.Length - 1
                ? normalizedPath
                : string.Join('/', segments, 0, depth + 1);

            var pathIsDirectory = depth < segments.Length - 1 || isDirectory;

            var isExcluded =
                MatchIgnoreFiles(ProtonIgnoreFileName, rootLocalPath, segments, depth, path, pathIsDirectory) ??
                (_honoursGitIgnoreFiles
                    ? MatchIgnoreFiles(GitIgnoreFileName, rootLocalPath, segments, depth, path, pathIsDirectory)
                    : null);

            if (isExcluded == true)
            {
                _logger.LogDebug(
                    "Item \"{Path}\" of sync root {RootId} is excluded by an ignore rule",
                    path,
                    rootId);

                return true;
            }
        }

        return false;
    }

    public void Invalidate()
    {
        _cache.Clear();
    }

    private bool? MatchIgnoreFiles(string fileName, string rootLocalPath, string[] segments, int depth, string path, bool isDirectory)
    {
        bool? result = null;

        // An ignore file governs its own directory and everything below, a deeper one overrides a shallower one
        for (var baseDepth = 0; baseDepth <= depth; baseDepth++)
        {
            var basePath = baseDepth == 0 ? string.Empty : string.Join('/', segments, 0, baseDepth);

            var filePath = basePath.Length == 0
                ? Path.Combine(rootLocalPath, fileName)
                : Path.Combine(rootLocalPath, basePath.Replace('/', Path.DirectorySeparatorChar), fileName);

            var match = GetRuleSet(filePath, basePath).Match(path, isDirectory);

            if (match.HasValue)
            {
                result = match;
            }
        }

        return result;
    }

    private IgnoreRuleSet GetRuleSet(string filePath, string basePath)
    {
        var now = DateTime.UtcNow;

        if (_cache.TryGetValue(filePath, out var entry) && now - entry.LastCheckedUtc < RevalidationInterval)
        {
            return entry.RuleSet;
        }

        var fileInfo = new FileInfo(filePath);
        var exists = fileInfo.Exists;
        var lastWriteTimeUtc = exists ? fileInfo.LastWriteTimeUtc : default;
        var length = exists ? fileInfo.Length : -1L;

        if (entry is not null && entry.Exists == exists && entry.LastWriteTimeUtc == lastWriteTimeUtc && entry.Length == length)
        {
            entry.LastCheckedUtc = now;

            return entry.RuleSet;
        }

        var ruleSet = Read(filePath, basePath, exists, length);

        _cache[filePath] = new CacheEntry
        {
            RuleSet = ruleSet,
            Exists = exists,
            LastWriteTimeUtc = lastWriteTimeUtc,
            Length = length,
            LastCheckedUtc = now,
        };

        return ruleSet;
    }

    private IgnoreRuleSet Read(string filePath, string basePath, bool exists, long length)
    {
        if (!exists)
        {
            return IgnoreRuleSet.Empty;
        }

        if (length > MaxIgnoreFileSizeInBytes)
        {
            _logger.LogWarning(
                "Ignore file is larger than {MaxSize} bytes and is not applied",
                MaxIgnoreFileSizeInBytes);

            return IgnoreRuleSet.Empty;
        }

        try
        {
            var ruleSet = IgnoreRuleSet.Parse(basePath, File.ReadLines(filePath));

            _logger.LogDebug("Loaded ignore file with base path \"{BasePath}\"", basePath);

            return ruleSet;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Failed to read ignore file with base path \"{BasePath}\"", basePath);

            return IgnoreRuleSet.Empty;
        }
    }

    private sealed class CacheEntry
    {
        public required IgnoreRuleSet RuleSet { get; init; }
        public required bool Exists { get; init; }
        public required DateTime LastWriteTimeUtc { get; init; }
        public required long Length { get; init; }
        public DateTime LastCheckedUtc { get; set; }
    }
}
