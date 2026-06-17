using ProtonDrive.App.Onboarding;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Sync;

internal sealed class SyncLifecycleController : IOnboardingStateAware
{
    private readonly ISyncLifecycleService _syncLifecycleService;

    private bool _onboardingIsCompleted;

    public SyncLifecycleController(ISyncLifecycleService syncLifecycleService)
    {
        _syncLifecycleService = syncLifecycleService;
    }

    void IOnboardingStateAware.OnboardingStateChanged(OnboardingState value)
    {
        if (!ValueExtensions.TryUpdate(ref _onboardingIsCompleted, value.Status is OnboardingStatus.Completed))
        {
            return;
        }

        if (_onboardingIsCompleted)
        {
            _syncLifecycleService.Enable();
        }
        else
        {
            _syncLifecycleService.Disable();
        }
    }
}
