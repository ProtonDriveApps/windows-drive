using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Account;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Extensions;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.Toolkit.Threading;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal sealed class UpgradeStorageStepViewModel : OnboardingStepViewModel, IUserStateAware
{
    private readonly IOnboardingService _onboardingService;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IUpgradeStoragePlanAvailabilityVerifier _upgradeStoragePlanAvailabilityVerifier;
    private readonly DispatcherScheduler _scheduler;
    private readonly IReadOnlyList<StorageUpgradeOffer> _allOffers;
    private readonly ObservableCollection<StorageUpgradeOffer> _relevantOffers = new();

    private UserState _user = UserState.Empty;

    public UpgradeStorageStepViewModel(
        IOnboardingService onboardingService,
        IExternalHyperlinks externalHyperlinks,
        IUpgradeStoragePlanAvailabilityVerifier upgradeStoragePlanAvailabilityVerifier,
        DispatcherScheduler scheduler)
        : base(scheduler)
    {
        _onboardingService = onboardingService;
        _externalHyperlinks = externalHyperlinks;
        _upgradeStoragePlanAvailabilityVerifier = upgradeStoragePlanAvailabilityVerifier;
        _scheduler = scheduler;

        UpgradeCommand = new RelayCommand(OpenWebPageAndContinue);
        SkipCommand = new RelayCommand(CompleteStep);

        _allOffers = new StorageUpgradeOffer[]
        {
            new("Unlimited", StorageInGb: 500, NumberOfUsers: 1, IsRecommended: true, UpgradeCommand),
            new("Family", StorageInGb: 3000, NumberOfUsers: 6, IsRecommended: false, UpgradeCommand),
        };

        RelevantOffers = new(_relevantOffers);
    }

    public ReadOnlyObservableCollection<StorageUpgradeOffer> RelevantOffers { get; }

    public ICommand SkipCommand { get; }

    private ICommand UpgradeCommand { get; }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        if (!_onboardingService.IsUpgradeStorageStepCompleted() && _user.MaxSpace != value.MaxSpace)
        {
            _scheduler.Schedule(() => UpdateAvailableOffers(value.MaxSpace, value.SubscriptionPlanCode));
        }

        _user = value;
    }

    protected override void Activate()
    { }

    protected override bool SkipActivation()
    {
        return true;
    }

    private void UpdateAvailableOffers(long availableStorageSpace, string? planCode)
    {
        if (!_upgradeStoragePlanAvailabilityVerifier.UpgradedPlanIsAvailable(UpgradeStoragePlanMode.Onboarding, planCode))
        {
            return;
        }

        var availableStorageSpaceInGb = availableStorageSpace / 1_000_000_000;
        _relevantOffers.Clear();
        _relevantOffers.AddEach(_allOffers.Where(x => x.StorageInGb > availableStorageSpaceInGb));

        if (_relevantOffers.Count == 0)
        {
            return;
        }

        IsActive = true;
    }

    private void OpenWebPageAndContinue()
    {
        _externalHyperlinks.UpgradePlanFromOnboarding.Open();
        CompleteStep();
    }

    private void CompleteStep()
    {
        IsActive = false;
        _onboardingService.SetUpgradeStorageStepCompleted();
    }
}
