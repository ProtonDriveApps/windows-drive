namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal enum ItemExclusionDecision
{
    /// <summary>
    /// The item takes part in synchronization.
    /// </summary>
    Include,

    /// <summary>
    /// The item is excluded by a built-in rule, for example because it is a temporary file or a
    /// special folder. An item that was known before is reported to the Sync Engine as deleted.
    /// </summary>
    ExcludeAlways,

    /// <summary>
    /// The item is excluded by a user defined ignore rule. Such a rule is only applied to items the
    /// adapter has not indexed yet, so that enabling a rule never deletes anything.
    /// </summary>
    ExcludeByUserRule,
}
