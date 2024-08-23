using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Account;

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

    private User? _cachedUser;
    private Organization? _cachedOrganization;
    private UserSubscription? _cachedSubscription;
    private Plan? _cachedDefaultPlan;

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

    public void ApplyUpdate(User? user, Organization? organization, UserSubscription? subscription, long? usedSpace)
    {
        _userStateUpdate.Cancel();

        if (user is not null)
        {
            _cachedUser = user;
        }

        if (organization is not null)
        {
            _cachedOrganization = organization;
        }

        if (subscription is not null)
        {
            _cachedSubscription = subscription;
        }

        var cachedUser = _cachedUser;
        if (usedSpace is not null && cachedUser is not null)
        {
            cachedUser.UsedDriveSpace = usedSpace.Value;
        }

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
        // Wait for the current scheduled task to complete
        return _userStateUpdate.CurrentTask;
    }

    private static UserState ToUserState(User user, UserSubscriptionPlan userSubscriptionPlan)
    {
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
            UsedSpace = user.UsedDriveSpace,
            SubscriptionPlanCode = userSubscriptionPlan.Code,
            SubscriptionPlanDisplayName = userSubscriptionPlan.DisplayName,
            OrganizationDisplayName = userSubscriptionPlan.OrganizationDisplayName,
        };
    }

    private async Task UpdateUserStateAsync(CancellationToken cancellationToken)
    {
        var user = _cachedUser;

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

            _cachedUser = user;
        }

        await SafeGetPlanName(user, cancellationToken).ConfigureAwait(false);

        var userState = ToUserState(user, _cachedSubscriptionPlan);

        SetUserState(userState);
    }

    private async Task SafeGetPlanName(User user, CancellationToken cancellationToken)
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
            return default;
        }

        var organization = _cachedOrganization;

        if (organization is null)
        {
            var organizationResponse = await _userApiClient.GetOrganizationAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            organization = organizationResponse.Organization;
            _cachedOrganization = organization;
        }

        return new UserSubscriptionPlan(organization.PlanCode, default, organization.DisplayName);
    }

    private async Task<UserSubscriptionPlan?> GetAccountPlanNameAsync(User user, CancellationToken cancellationToken)
    {
        if (user.HasNoSubscription()
            || user.Type == UserType.Managed
            || !_hasPaymentsScope)
        {
            return default;
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
                .Select(x => new UserSubscriptionPlan(x.Code, x.DisplayName, default))
                .FirstOrDefault();
        }
        catch (ApiException ex) when (ex.ResponseCode == ResponseCode.NoActiveSubscription)
        {
            // Response code NoActiveSubscription indicates the user account has no subscriptions, therefore is "free"
            return default;
        }
    }

    private async Task<UserSubscriptionPlan?> GetDefaultPlanNameAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Type == UserType.Managed)
        {
            return default;
        }

        var defaultPlan = _cachedDefaultPlan;

        if (defaultPlan is null)
        {
            var defaultPlanResponse = await _paymentsApiClient.GetDefaultPlanAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            defaultPlan = defaultPlanResponse.Plan;
            _cachedDefaultPlan = defaultPlan;
        }

        return new UserSubscriptionPlan(defaultPlan.Code, defaultPlan.DisplayName, default);
    }

    private void ClearCache()
    {
        _userClient.ClearCache();
        _userState = UserState.Empty;
        _cachedSubscriptionPlan = UserSubscriptionPlan.Empty;
        _cachedUser = default;
        _cachedOrganization = default;
        _cachedSubscription = default;
        _cachedDefaultPlan = default;
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

    private record UserSubscriptionPlan(string Code, string? DisplayName, string? OrganizationDisplayName)
    {
        public static UserSubscriptionPlan Empty { get; } = new(string.Empty, default, default);
    }
}
