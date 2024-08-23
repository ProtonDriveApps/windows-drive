using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Account;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Toolkit.Threading;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal abstract class OnboardingStepViewModel : ObservableObject, ISessionStateAware, IAccountSwitchingAware
{
    protected static readonly TimeSpan DelayBeforeSwitchingStep = TimeSpan.FromMilliseconds(900);

    private readonly DispatcherScheduler _scheduler;

    private bool _isActive;

    protected OnboardingStepViewModel(DispatcherScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public bool IsActive
    {
        get => _isActive;
        protected set => SetProperty(ref _isActive, value);
    }

    protected SessionState SessionState { get; private set; } = SessionState.None;

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        Schedule(() =>
        {
            SessionState = value;

            OnSessionStateChanged();
            HandleExternalStateChange();
        });
    }

    void IAccountSwitchingAware.OnAccountSwitched()
    {
        Schedule(HandleExternalStateChange);
    }

    protected abstract void Activate();

    protected abstract bool SkipActivation();

    protected virtual void OnSessionStateChanged() { }

    private void HandleExternalStateChange()
    {
        if (SessionState.Status is SessionStatus.Ending or SessionStatus.NotStarted)
        {
            IsActive = false;

            return;
        }

        if (IsActive)
        {
            return;
        }

        if (SkipActivation())
        {
            return;
        }

        Activate();
    }

    private void Schedule(Action origin)
    {
        _scheduler.Schedule(origin);
    }
}
