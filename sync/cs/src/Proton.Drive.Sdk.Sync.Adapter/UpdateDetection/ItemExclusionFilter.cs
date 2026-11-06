using System.Collections.Frozen;
using Proton.Drive.Sdk.Sync.Shared.Ignore;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal sealed class ItemExclusionFilter : IItemExclusionFilter
{
    private static readonly FrozenSet<string> FileExtensionsToIgnore = new[]
    {
        ".crdownload",
        ".download",
        ".partial",
        ".part",
        ".temp",
        ".tmp",
        ".~tmp",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> FolderNamesToIgnore = new[]
    {
        ".tmp.driveupload",     // Used by Google Drive
        ".tmp.drivedownload",   // Used by Google Drive
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyCollection<string> _specialFolderNames;
    private readonly IIgnoreRuleProvider _ignoreRuleProvider;

    public ItemExclusionFilter(IReadOnlyCollection<string> specialFolderNames, IIgnoreRuleProvider ignoreRuleProvider)
    {
        _specialFolderNames = specialFolderNames;
        _ignoreRuleProvider = ignoreRuleProvider;
    }

    public bool HonoursUserRules => _ignoreRuleProvider.IsEnabled;

    public ItemExclusionDecision GetDecision(
        string name,
        FileAttributes attributes,
        PlaceholderState placeholderState,
        bool parentIsSyncRoot,
        IgnoreRuleScope ignoreRuleScope)
    {
        if (ShouldBeIgnored(name, attributes, placeholderState) || (parentIsSyncRoot && ShouldBeIgnoredOnSyncRoot()))
        {
            return ItemExclusionDecision.ExcludeAlways;
        }

        if (MatchesUserRule())
        {
            return ItemExclusionDecision.ExcludeByUserRule;
        }

        return ItemExclusionDecision.Include;

        bool ShouldBeIgnoredOnSyncRoot()
        {
            // Special Proton Drive folders on the replica root are ignored
            return _specialFolderNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        bool MatchesUserRule()
        {
            return _ignoreRuleProvider.IsEnabled
                   && ignoreRuleScope.IsDefined
                   && _ignoreRuleProvider.IsIgnored(
                       ignoreRuleScope.RootId,
                       ignoreRuleScope.RootLocalPath,
                       ignoreRuleScope.RelativePath,
                       attributes.HasFlag(FileAttributes.Directory));
        }
    }

    private bool ShouldBeIgnored(string name, FileAttributes attributes, PlaceholderState placeholderState)
    {
        return placeholderState.HasFlag(PlaceholderState.Invalid)
               || attributes.HasFlag(FileAttributes.Device)
               || (attributes.HasFlag(FileAttributes.ReparsePoint) && !placeholderState.HasFlag(PlaceholderState.Placeholder))
               || attributes.HasFlag(FileAttributes.Temporary)
               || IsSystemFile()
               || IsProtectedSystemFolder()
               || IsMicrosoftOrLibreOfficeTemporaryFile()
               || IsWellKnownTemporaryFile()
               || IsWellKnownTemporaryFolder()
            ;

        bool IsSystemFile()
        {
            return attributes.HasFlag(FileAttributes.System) && !attributes.HasFlag(FileAttributes.Directory);
        }

        bool IsProtectedSystemFolder()
        {
            return attributes.HasFlag(FileAttributes.System) && attributes.HasFlag(FileAttributes.Hidden) && attributes.HasFlag(FileAttributes.Directory);
        }

        bool IsMicrosoftOrLibreOfficeTemporaryFile()
        {
            return !attributes.HasFlag(FileAttributes.Directory)
                   && // Used by Microsoft Office
                   ((name.StartsWith("~", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) ||
                    name.StartsWith("~$", StringComparison.Ordinal)
                    || // Used by Libre Office
                    name.StartsWith(".~", StringComparison.Ordinal));
        }

        bool IsWellKnownTemporaryFile()
        {
            return !attributes.HasFlag(FileAttributes.Directory)
                   && FileExtensionsToIgnore.Contains(Path.GetExtension(name));
        }

        bool IsWellKnownTemporaryFolder()
        {
            return attributes.HasFlag(FileAttributes.Directory)
                && FolderNamesToIgnore.Contains(name);
        }
    }
}
