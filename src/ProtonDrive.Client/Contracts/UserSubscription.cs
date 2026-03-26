using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record UserSubscription
{
    private ImmutableArray<Plan>? _plans;

    [JsonPropertyName("ID")]
    public string? Id { get; init; }

    [JsonPropertyName("InvoiceID")]
    public string? InvoiceId { get; init; }

    [JsonPropertyName("External")]
    public UserSubscriptionChannel Channel { get; init; }

    public string? CouponCode { get; init; }

    public int Cycle { get; init; }

    public long PeriodStart { get; init; }

    public long PeriodEnd { get; init; }

    public ImmutableArray<Plan> Plans
    {
        get => _plans ??= [];
        init => _plans = value;
    }
}
