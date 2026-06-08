using System.Text.Json.Serialization;

namespace ProtonDrive.App.Notifications.Contracts;

public sealed class Notification
{
    [JsonPropertyName("NotificationID")]
    public required string Id { get; init; }

    public IReadOnlyCollection<string> UserSubscriptionPlanCodes { get; init; } = [];
    public IReadOnlyCollection<string> ExcludedUserSubscriptionPlanCouponCodes { get; init; } = [];
    public int? UserSubscriptionCycle { get; init; }
    public bool? Retention { get; init; }
    public string? UserCurrency { get; init; }

    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }

    public NotificationType Type { get; init; }

    public string? HeaderText { get; set; }
    public string? ContentText { get; set; }
    public string? ButtonText { get; set; }
    public string? LogoImageUrl { get; init; }

    public Offer? Offer { get; init; }
}
