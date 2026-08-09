namespace Proton.Drive.Sdk.Sync.Shared.Ignore;

/// <summary>
/// An ignore rule provider that never excludes anything. Used on the remote replica, where user
/// defined ignore rules must not be applied, as excluding a remote item would delete the local one.
/// </summary>
public sealed class NullIgnoreRuleProvider : IIgnoreRuleProvider
{
    public static readonly NullIgnoreRuleProvider Instance = new();

    private NullIgnoreRuleProvider()
    {
    }

    public bool IsEnabled => false;

    public bool IsIgnored(int rootId, string rootLocalPath, string relativePath, bool isDirectory) => false;

    public void Invalidate()
    {
    }
}
