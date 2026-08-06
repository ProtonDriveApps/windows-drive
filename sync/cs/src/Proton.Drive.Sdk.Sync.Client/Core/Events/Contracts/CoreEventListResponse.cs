using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Client.Settings.Contracts;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Core.Events.Contracts;

internal sealed record CoreEventListResponse : ApiResponse
{
    private IImmutableList<AddressEvent>? _addresses;
    private long? _usedDriveSpace;

    [JsonPropertyName("EventID")]
    public string? AnchorId { get; init; }

    [JsonPropertyName("More")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool HasMoreData { get; init; }

    [JsonPropertyName("Refresh")]
    public CoreEventsRefreshMask RefreshMask { get; init; }

    [JsonPropertyName("Addresses")]
    public IImmutableList<AddressEvent> AddressEvents
    {
        get => _addresses ??= ImmutableList<AddressEvent>.Empty;
        init => _addresses = value;
    }

    public User? User { get; init; }

    public Organization? Organization { get; init; }

    public UserSubscription? Subscription { get; init; }

    public GeneralSettings? UserSettings { get; init; }

    public long? UsedSpace { get; init; }

    [JsonPropertyName("UsedDriveSpace")]
    public long? SplitStorageUsedSpace
    {
        get => _usedDriveSpace ?? UsedSpace;
        set => _usedDriveSpace = value;
    }

    public ProductUsedSpace ProductUsedSpace { get; init; } = ProductUsedSpace.Empty;
}
