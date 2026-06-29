using CommunityToolkit.Mvvm.ComponentModel;

namespace Proton.Drive.App.Windows.Views.Onboarding;

internal abstract class OnboardingStepViewModel : ObservableObject
{
    private static readonly TimeSpan DelayBeforeSwitchingStep = TimeSpan.FromMilliseconds(900);

    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        private set => SetProperty(ref _isActive, value);
    }

    public virtual void Activate()
    {
        IsActive = true;
    }

    public virtual void Deactivate()
    {
        IsActive = false;
    }

    protected static Task DelayBeforeSwitchingStepAsync()
    {
        return Task.Delay(DelayBeforeSwitchingStep);
    }
}
