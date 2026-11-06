using Proton.Drive.Sdk.Sync.Shared.Ignore;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal interface IItemExclusionFilter
{
    /// <summary>
    /// Whether user defined ignore rules are honoured. When false, callers should not build the
    /// <see cref="IgnoreRuleScope"/>, as walking the Adapter Tree to compose the path is not free.
    /// </summary>
    public bool HonoursUserRules { get; }

    public ItemExclusionDecision GetDecision(
        string name,
        FileAttributes attributes,
        PlaceholderState placeholderState,
        bool parentIsSyncRoot,
        IgnoreRuleScope ignoreRuleScope);
}
