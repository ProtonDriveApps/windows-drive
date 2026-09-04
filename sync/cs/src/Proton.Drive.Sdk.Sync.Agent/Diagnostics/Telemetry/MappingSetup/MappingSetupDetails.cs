using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.MappingSetup;

public sealed record MappingSetupDetails
{
    public MappingSetupDetails(
        MappingType type,
        LinkType linkType,
        SyncMethod syncMethod,
        MappingStatus status,
        MappingSetupStatus mappingSetupStatus,
        bool isReadOnly,
        OpenByFileIdSupportStatus openByFileIdSupportStatus)
    {
        Type = type;
        LinkType = linkType;
        SyncMethod = syncMethod;
        Status = status;
        SetupStatus = mappingSetupStatus;
        OpenByFileIdSupportStatus = openByFileIdSupportStatus;
        SyncType = type switch
        {
            MappingType.SharedWithMeItem when isReadOnly => MappingSyncType.OneWayToLocal,
            _ => MappingSyncType.TwoWay,
        };
    }

    public MappingType Type { get; }
    public LinkType LinkType { get; }
    public SyncMethod SyncMethod { get; }
    public MappingStatus Status { get; }
    public MappingSetupStatus SetupStatus { get; }
    public MappingSyncType SyncType { get; }
    public OpenByFileIdSupportStatus OpenByFileIdSupportStatus { get; }
}
