using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Authentication;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Account;

internal sealed class UserService : IUserService, IDisposable
{
    public const string PaymentsScopeName = "payments";

    private readonly IUserClient _userClient;
    private readonly IUserApiClient _userApiClient;
    private readonly IPaymentsApiClient _paymentsApiClient;
    private readonly Lazy<IEnumerable<IUserStateAware>> _userStateAware;
    private readonly ILogger<UserService> _logger;

    private readonly SingleAction _userStateUpdate;
    private readonly ISchedulerTimer _timer;

    private bool _hasPaymentsScope;
    private UserSubscriptionPlan _cachedSubscriptionPlan = UserSubscriptionPlan.Empty;
    private UserState _userState = UserState.Empty;

    private Organization? _cachedOrganization;
    private UserSubscription? _cachedSubscription;
    private Plan? _cachedDefaultPlan;
    private DateTime? _cachedLatestSubscriptionCancellationTimeUtc;

    public UserService(
        AppConfig appConfig,
        IScheduler scheduler,
        IUserClient userClient,
        IUserApiClient userApiClient,
        IPaymentsApiClient paymentsApiClient,
        Lazy<IEnumerable<IUserStateAware>> userStateAware,
        ILogger<UserService> logger)
    {
        _userClient = userClient;
        _userApiClient = userApiClient;
        _paymentsApiClient = paymentsApiClient;
        _userStateAware = userStateAware;
        _logger = logger;

        _userStateUpdate = new SingleAction(cancellationToken => WithLoggedExceptions(() => WithSafeCancellation(UpdateUserStateAsync, cancellationToken)));

        _timer = scheduler.CreateTimer();
        _timer.Interval = appConfig.UserUpdateInterval.RandomizedWithDeviation(0.2);
        _timer.Tick += OnTimerTick;
    }

    public void Start(IReadOnlyCollection<string> sessionScopes)
    {
        _hasPaymentsScope = sessionScopes.Any(x => x.Equals(PaymentsScopeName, StringComparison.OrdinalIgnoreCase));
        ClearCache();
        _timer.Start();

        _logger.LogInformation($"{nameof(UserService)} started");
    }

    public async Task StopAsync()
    {
        _logger.LogInformation($"{nameof(UserService)} is stopping");

        _timer.Stop();
        _userStateUpdate.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(UserService)} stopped");
    }

    public async Task<UserState> GetUserAsync(CancellationToken cancellationToken)
    {
        if (_userState.IsEmpty)
        {
            await _userStateUpdate.RunAsync().WithCancellation(cancellationToken).ConfigureAwait(false);
        }

        return _userState;
    }

    public void ApplyUpdate(User? user, Organization? organization, UserSubscription? subscription, long? usedSpace, long? driveUsedSpace)
    {
        _userStateUpdate.Cancel();

        if (user is not null)
        {
            _userClient.SetCachedUser(user);
        }

        if (organization is not null)
        {
            _cachedOrganization = organization;
        }

        if (subscription is not null)
        {
            _cachedSubscription = subscription;
        }

        _userClient.UpdateCachedUserQuota(usedSpace, driveUsedSpace);

        _userStateUpdate.RunAsync();
    }

    public void Refresh()
    {
        ClearCache();

        _userStateUpdate.RunAsync();
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    internal Task WaitForCompletionAsync()
    {
        return _userStateUpdate.WaitForCompletionAsync();
    }

    private UserState ToUserState(User user, UserSubscriptionPlan userSubscriptionPlan, DateTime? latestSubscriptionCancellationTimeUtc)
    {
        if (latestSubscriptionCancellationTimeUtc == default(DateTime))
        {
            latestSubscriptionCancellationTimeUtc = null;
        }

        return new UserState
        {
            Id = user.Id,
            Type = user.Type,
            Name = !string.IsNullOrEmpty(user.Name) ? user.Name : string.Empty, // Name is optional
            DisplayName = !string.IsNullOrEmpty(user.DisplayName)
                ? user.DisplayName
                : !string.IsNullOrEmpty(user.Name)
                    ? user.Name
                    : user.EmailAddress, // DisplayName and Name are optional
            EmailAddress = user.EmailAddress,
            IsDelinquent = user.IsDelinquent,
            MaxSpace = user.MaxDriveSpace,
            UsedSpace = user.SplitStorageUsedSpace,
            UsedDriveSpace = user.ProductUsedSpace.Drive,
            Currency = user.Currency,
            SubscriptionPlanCode = userSubscriptionPlan.Code,
            SubscriptionCycle = userSubscriptionPlan.Cycle,
            SubscriptionPlanDisplayName = userSubscriptionPlan.DisplayName,
            SubscriptionPlanCouponCode = userSubscriptionPlan.CouponCode,
            LatestSubscriptionCancellationTimeUtc = latestSubscriptionCancellationTimeUtc,
            OrganizationDisplayName = userSubscriptionPlan.OrganizationDisplayName,
            CanBuySubscription = _hasPaymentsScope && user.Type != UserType.Managed && !userSubscriptionPlan.ExternalChannel,
        };
    }

    private async Task UpdateUserStateAsync(CancellationToken cancellationToken)
    {
        var user = _userClient.GetCachedUser();

        if (user is null)
        {
            try
            {
                user = await _userClient.GetUserAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ApiException)
            {
                return;
            }
        }

        await SafeGetPlanNameAsync(user, cancellationToken).ConfigureAwait(false);

        await SafeGetLatestSubscriptionCancellationAsync(cancellationToken).ConfigureAwait(false);

        var userState = ToUserState(user, _cachedSubscriptionPlan, _cachedLatestSubscriptionCancellationTimeUtc);

        SetUserState(userState);
    }

    private async Task SafeGetPlanNameAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var userPlan = await GetAccountPlanNameAsync(user, cancellationToken).ConfigureAwait(false);

            if (userPlan is not null)
            {
                _cachedSubscriptionPlan = userPlan;
                return;
            }

            var organizationPlan = await GetOrganizationNameAsync(user, cancellationToken).ConfigureAwait(false);

            if (organizationPlan is not null && !string.IsNullOrEmpty(organizationPlan.OrganizationDisplayName))
            {
                _cachedSubscriptionPlan = organizationPlan;
                return;
            }

            var defaultPlan = await GetDefaultPlanNameAsync(user, cancellationToken).ConfigureAwait(false);

            if (defaultPlan is not null)
            {
                _cachedSubscriptionPlan = defaultPlan;
            }
        }
        catch (ApiException)
        {
            // Ignore
        }
    }

    private async Task<UserSubscriptionPlan?> GetOrganizationNameAsync(User user, CancellationToken cancellationToken)
    {
        if (user.HasNoSubscription())
        {
            return null;
        }

        var organization = _cachedOrganization;

        if (organization is null)
        {
            var organizationResponse = await _userApiClient.GetOrganizationAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            organization = organizationResponse.Organization;
            _cachedOrganization = organization;
        }

        return new UserSubscriptionPlan(
            organization.PlanCode,
            DisplayName: null,
            organization.DisplayName,
            CouponCode: null,
            Cycle: 0,
            ExternalChannel: false);
    }

    private async Task<UserSubscriptionPlan?> GetAccountPlanNameAsync(User user, CancellationToken cancellationToken)
    {
        if (user.HasNoSubscription()
            || user.Type == UserType.Managed
            || !_hasPaymentsScope)
        {
            return null;
        }

        try
        {
            var subscription = _cachedSubscription;

            if (subscription is null)
            {
                var subscriptionResponse = await _paymentsApiClient.GetSubscriptionAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

                subscription = subscriptionResponse.Subscription;
                _cachedSubscription = subscription;
            }

            return subscription.Plans
                .Where(x => x.Type == PlanType.PrimaryPlan)
                .Select(x => new UserSubscriptionPlan(
                    x.Code,
                    x.DisplayName,
                    null,
                    subscription.CouponCode,
                    subscription.Cycle,
                    subscription.Channel is not UserSubscriptionChannel.Proton))
                .FirstOrDefault();
        }
        catch (ApiException ex) when (ex.ResponseCode == ResponseCode.NoActiveSubscription)
        {
            // Response code NoActiveSubscription indicates the user account has no subscriptions, therefore is "free"
            return null;
        }
    }

    private async Task<UserSubscriptionPlan?> GetDefaultPlanNameAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Type == UserType.Managed)
        {
            return null;
        }

        var defaultPlan = _cachedDefaultPlan;

        if (defaultPlan is null)
        {
            var defaultPlanResponse = await _paymentsApiClient.GetDefaultPlanAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            defaultPlan = defaultPlanResponse.Plan;
            _cachedDefaultPlan = defaultPlan;
        }

        return new UserSubscriptionPlan(
            defaultPlan.Code,
            defaultPlan.DisplayName,
            OrganizationDisplayName: null,
            CouponCode: null,
            Cycle: 0,
            ExternalChannel: false);
    }

    private async Task SafeGetLatestSubscriptionCancellationAsync(CancellationToken cancellationToken)
    {
        if (_cachedLatestSubscriptionCancellationTimeUtc is not null)
        {
            return;
        }

        try
        {
            var response = await _paymentsApiClient.GetLatestSubscriptionAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            _cachedLatestSubscriptionCancellationTimeUtc = response.CancellationTimeUtc ?? default(DateTime);
        }
        catch (ApiException)
        {
            // Ignore
        }
    }

    private void ClearCache()
    {
        _userClient.ClearCache();
        _userState = UserState.Empty;
        _cachedSubscriptionPlan = UserSubscriptionPlan.Empty;
        _cachedOrganization = null;
        _cachedSubscription = null;
        _cachedDefaultPlan = null;
        _cachedLatestSubscriptionCancellationTimeUtc = null;
    }

    private void SetUserState(UserState userState)
    {
        _userState = userState;

        OnUserStateChanged(userState);
    }

    private void OnUserStateChanged(UserState value)
    {
        foreach (var listener in _userStateAware.Value)
        {
            listener.OnUserStateChanged(value);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_userState != UserState.Empty)
        {
            return;
        }

        _userStateUpdate.RunAsync();
    }

    private Task WithLoggedExceptions(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, $"{nameof(UserService)} operation failed", includeStackTrace: true);
    }

    private async Task WithSafeCancellation(Func<CancellationToken, Task> origin, CancellationToken cancellationToken)
    {
        try
        {
            await origin.Invoke(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
            _logger.LogInformation($"{nameof(UserService)} operation was cancelled");
        }
    }

    private record UserSubscriptionPlan(
        string? Code,
        string? DisplayName,
        string? OrganizationDisplayName,
        string? CouponCode,
        int Cycle,
        bool ExternalChannel)
    {
        public static UserSubscriptionPlan Empty { get; } = new(
            Code: null,
            DisplayName: null,
            OrganizationDisplayName: null,
            CouponCode: null,
            Cycle: 0,
            ExternalChannel: false);
    }
}
