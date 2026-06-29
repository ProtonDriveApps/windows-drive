using System.Text;
using System.Text.RegularExpressions;
using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Agent.Account;

public sealed partial record UserState
{
    private const string StartOrSpaceLookbehindPattern = @"(?<=^|\s)";
    private const string StartOrSpaceLookaheadPattern = @"(?=$|\s)";
    private const string NamePattern = @"[^.,/#!$@%^&*;:{}=\-_`~()\s][^\s]*";
    private const string MatchPattern = StartOrSpaceLookbehindPattern + NamePattern + StartOrSpaceLookaheadPattern;

    private static readonly Regex NameRegex = GetNameRegex();

    public string Id { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? EmailAddress { get; init; }

    public UserType Type { get; init; }

    public string? SubscriptionPlanCode { get; init; }

    public int SubscriptionCycle { get; init; }

    /// <summary>
    /// Subscription plan name of the unmanaged user account.
    /// </summary>
    public string? SubscriptionPlanDisplayName { get; init; }

    /// <summary>
    /// Coupon code used for buying subscription plan.
    /// </summary>
    public string? SubscriptionPlanCouponCode { get; init; }

    /// <summary>
    /// Date and time of last subscription cancellation, if any.
    /// </summary>
    public DateTime? LatestSubscriptionCancellationTimeUtc { get; init; }

    /// <summary>
    /// Organization name of the managed user account.
    /// <remarks>
    /// The null value indicates that the account is an unmanaged account.</remarks>
    /// </summary>
    public string? OrganizationDisplayName { get; init; }

    public bool CanBuySubscription { get; init; }

    public bool IsDelinquent { get; init; }

    public long UsedSpace { get; init; }

    public long UsedDriveSpace { get; init; }

    public long MaxSpace { get; init; }

    public string? Currency { get; init; }

    public string Initials => field ??= GetInitials();

    public UserQuotaStatus UserQuotaStatus => MaxSpace == 0
        ? UserQuotaStatus.Regular
        : (100 * UsedSpace / MaxSpace) switch
        {
            >= 100 => UserQuotaStatus.LimitExceeded,
            >= 90 => UserQuotaStatus.WarningLevel2Exceeded,
            >= 80 => UserQuotaStatus.WarningLevel1Exceeded,
            _ => UserQuotaStatus.Regular,
        };

    public static UserState Empty { get; } = new();

    public bool IsEmpty => string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Name);

    private string GetInitials()
    {
        var matches = NameRegex.Matches(DisplayName ?? string.Empty);

        switch (matches.Count)
        {
            case 0:
                return "?";

            case 1:
                return GetCapitalizedFirstCharacter(matches[0].Value);

            default:
                var firstName = matches[0].Value;
                var lastName = matches[^1].Value;
                return string.Concat(GetCapitalizedFirstCharacter(firstName), GetCapitalizedFirstCharacter(lastName));
        }

        static string GetCapitalizedFirstCharacter(string word)
        {
            var rune = Rune.GetRuneAt(word, 0);
            return rune.ToString().ToUpper();
        }
    }

    [GeneratedRegex(MatchPattern, RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();
}
