using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record UserSubscription
{
    private ImmutableArray<Plan>? _plans;

    [JsonPropertyName("ID")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("InvoiceID")]
    public string InvoiceId { get; init; } = string.Empty;

    public int Cycle { get; init; }

    public long PeriodStart { get; init; }

    public long PeriodEnd { get; init; }

    public ImmutableArray<Plan> Plans
    {
        get => _plans ??= ImmutableArray<Plan>.Empty;
        init => _plans = value;
    }
}
