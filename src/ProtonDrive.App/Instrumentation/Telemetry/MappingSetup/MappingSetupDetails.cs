using ProtonDrive.App.Mapping;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Instrumentation.Telemetry.MappingSetup;

public sealed record MappingSetupDetails
{
    public MappingSetupDetails(
        MappingType type,
        LinkType linkType,
        SyncMethod syncMethod,
        MappingStatus status,
        MappingSetupStatus mappingSetupStatus,
        bool isReadOnly)
    {
        Type = type;
        LinkType = linkType;
        SyncMethod = syncMethod;
        Status = status;
        SetupStatus = mappingSetupStatus;
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
}
