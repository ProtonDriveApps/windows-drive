using Proton.Drive.App.Onboarding;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared;

namespace Proton.Drive.App.Windows;

internal sealed class AppLifecycleService
{
    private SessionState _sessionState = SessionState.None;
    private OnboardingState _onboardingState = OnboardingState.Initial;
    private AppLifecycleState _state = AppLifecycleState.Initial;
    private bool _isFirstSessionStartAttempt = true;
    private bool _isSavedSessionStartAttempt = true;
    private bool _isQuiet;

    public AppLifecycleService(AppArguments appArguments)
    {
        _isQuiet = appArguments.LaunchMode is AppLaunchMode.Quiet;
    }

    public event EventHandler<AppLifecycleState>? StateChanged;

    public void SetSessionState(SessionState value)
    {
        if (_isFirstSessionStartAttempt && _sessionState.Status is SessionStatus.Starting && value.Status is not SessionStatus.Starting)
        {
            _isFirstSessionStartAttempt = false;
        }

        if (_isSavedSessionStartAttempt && value.Status is not SessionStatus.Starting and not SessionStatus.Failed)
        {
            _isSavedSessionStartAttempt = false;
        }

        _sessionState = value;
        RefreshState();
    }

    public void SetOnboardingState(OnboardingState value)
    {
        _onboardingState = value;
        RefreshState();
    }

    public void Activate()
    {
        _isQuiet = false;
        RefreshState(forceNotify: true);
    }

    private void RefreshState(bool forceNotify = false)
    {
        SetState(GetState(), forceNotify);
    }

    private void SetState(AppLifecycleState state, bool forceNotify)
    {
        if (!forceNotify && state == _state)
        {
            return;
        }

        var previousState = _state;
        _state = state;

        if (_isQuiet)
        {
            if (previousState.CurrentWindow is AppWindow.None || previousState.CurrentWindow == state.CurrentWindow)
            {
                // Prevent showing the first window if the app was started in quiet mode
                return;
            }

            _isQuiet = false;
        }

        StateChanged?.Invoke(this, state);
    }

    private AppLifecycleState GetState()
    {
        var window = GetActiveWindow();

        return new AppLifecycleState(window);
    }

    private AppWindow GetActiveWindow()
    {
        var sessionState = _sessionState;
        var onboardingState = _onboardingState;

        if ((!_isSavedSessionStartAttempt && sessionState.Status is SessionStatus.NotStarted or SessionStatus.Starting) ||
            sessionState.SigningInStatus is not SigningInStatus.None ||
            (sessionState.Status is SessionStatus.Ending && _state.CurrentWindow is AppWindow.SignIn))
        {
            return AppWindow.SignIn;
        }

        if (sessionState.Status is SessionStatus.Started &&
            onboardingState.Status is OnboardingStatus.Onboarding)
        {
            return AppWindow.Onboarding;
        }

        if ((!_isFirstSessionStartAttempt && _isSavedSessionStartAttempt) ||
            sessionState.Status is SessionStatus.Started)
        {
            return AppWindow.Main;
        }

        return AppWindow.None;
    }
}
