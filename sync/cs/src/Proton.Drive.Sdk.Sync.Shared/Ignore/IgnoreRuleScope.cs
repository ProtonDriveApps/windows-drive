namespace Proton.Drive.Sdk.Sync.Shared.Ignore;

/// <summary>
/// Identifies an item for the purpose of evaluating user defined ignore rules.
/// </summary>
/// <param name="RootId">The sync root (mapping) identity.</param>
/// <param name="RootLocalPath">The absolute path of the sync root on the local file system.</param>
/// <param name="RelativePath">The path of the item relative to the sync root.</param>
public readonly record struct IgnoreRuleScope(int RootId, string RootLocalPath, string RelativePath)
{
    /// <summary>
    /// The item cannot be matched against ignore rules, for example because it is a sync root itself.
    /// </summary>
    public static readonly IgnoreRuleScope None = new(0, string.Empty, string.Empty);

    public bool IsDefined => !string.IsNullOrEmpty(RelativePath) && !string.IsNullOrEmpty(RootLocalPath);
}
