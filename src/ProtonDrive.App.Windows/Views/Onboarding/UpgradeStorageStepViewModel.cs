using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Account;
using ProtonDrive.App.Notifications.Offers;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Extensions;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal sealed class UpgradeStorageStepViewModel : OnboardingStepViewModel, IUserStateAware, IOffersAware
{
    private readonly IOnboardingService _onboardingService;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IUpgradeStoragePlanAvailabilityVerifier _upgradeStoragePlanAvailabilityVerifier;
    private readonly DispatcherScheduler _scheduler;
    private readonly IReadOnlyList<StorageUpgradeOffer> _allOffers;
    private readonly ObservableCollection<StorageUpgradeOffer> _relevantOffers = [];

    private UserState _userState = UserState.Empty;

    private bool _activeOfferIsAvailable;

    public UpgradeStorageStepViewModel(
        IOnboardingService onboardingService,
        IExternalHyperlinks externalHyperlinks,
        IUpgradeStoragePlanAvailabilityVerifier upgradeStoragePlanAvailabilityVerifier,
        DispatcherScheduler scheduler)
    {
        _onboardingService = onboardingService;
        _externalHyperlinks = externalHyperlinks;
        _upgradeStoragePlanAvailabilityVerifier = upgradeStoragePlanAvailabilityVerifier;
        _scheduler = scheduler;

        UpgradeCommand = new RelayCommand(OpenUpgradeStorageLinkAndContinue);
        SkipCommand = new RelayCommand(CompleteStep);

        _allOffers =
        [
            new StorageUpgradeOffer(
                "Plus",
                StorageInGb: 200,
                NumberOfUsers: 1,
                IsRecommended: false,
                Price: 3.99,
                Resources.Strings.Onboarding_UpgradeStorage_Button_GetDrivePlus,
                UpgradeCommand),

            new StorageUpgradeOffer(
                "Unlimited",
                StorageInGb: 500,
                NumberOfUsers: 1,
                IsRecommended: true,
                Price: 9.99,
                Resources.Strings.Onboarding_UpgradeStorage_Button_GetUnlimited,
                UpgradeCommand),

            new StorageUpgradeOffer(
                "Duo",
                StorageInGb: 2000,
                NumberOfUsers: 2,
                IsRecommended: false,
                Price: 14.99,
                Resources.Strings.Onboarding_UpgradeStorage_Button_GetDuo,
                UpgradeCommand),
        ];

        RelevantOffers = new ReadOnlyObservableCollection<StorageUpgradeOffer>(_relevantOffers);
    }

    public ReadOnlyObservableCollection<StorageUpgradeOffer> RelevantOffers { get; }

    public string? UserCurrency => _userState.Currency?.ToUpperInvariant();

    public string? UserCurrencySymbol => UserCurrency switch { "USD" => "$", "EUR" => "€", _ => null };

    public bool OfferPriceIsVisible => UserCurrency is "EUR" or "USD";

    public ICommand SkipCommand { get; }

    private ICommand UpgradeCommand { get; }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        var previousState = _userState;
        _userState = value;

        if (!previousState.Currency?.Equals(UserCurrency, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            Schedule(() =>
            {
                OnPropertyChanged(nameof(UserCurrency));
                OnPropertyChanged(nameof(UserCurrencySymbol));
                OnPropertyChanged(nameof(OfferPriceIsVisible));
            });
        }

        if (previousState.MaxSpace != value.MaxSpace ||
            previousState.SubscriptionPlanCode != value.SubscriptionPlanCode)
        {
            Schedule(UpdateAvailableOffers);
        }
    }

    void IOffersAware.OnActiveOfferChanged(Notifications.Offers.Offer? offer)
    {
        if (!ValueExtensions.TryUpdate(ref _activeOfferIsAvailable, offer is not null))
        {
            return;
        }

        // TODO: the active offer may arrive after this step is already displayed,
        // in which case the step is shown even though it should have been skipped.
        Schedule(UpdateAvailableOffers);
    }

    public override void Activate()
    {
        base.Activate();

        Schedule(UpdateAvailableOffers);
    }

    public void CompleteStep()
    {
        _onboardingService.CompleteStep(OnboardingStep.UpgradeStorage);
    }

    private void UpdateAvailableOffers()
    {
        if (!IsActive)
        {
            return;
        }

        if (_activeOfferIsAvailable)
        {
            CompleteStep();
            return;
        }

        var userState = _userState;

        if (!_upgradeStoragePlanAvailabilityVerifier.UpgradedPlanIsAvailable(UpgradeStoragePlanMode.Onboarding, userState.SubscriptionPlanCode))
        {
            CompleteStep();
            return;
        }

        var availableStorageSpaceInGb = userState.MaxSpace / 1_000_000_000;
        _relevantOffers.Clear();
        _relevantOffers.AddEach(_allOffers.Where(x => x.StorageInGb > availableStorageSpaceInGb));

        if (_relevantOffers.Count == 0)
        {
            CompleteStep();
        }
    }

    private void OpenUpgradeStorageLinkAndContinue()
    {
        _externalHyperlinks.UpgradePlanFromOnboarding.Open();
        CompleteStep();
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
