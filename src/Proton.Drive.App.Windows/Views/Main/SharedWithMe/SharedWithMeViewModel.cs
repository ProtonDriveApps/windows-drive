using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Onboarding;

namespace Proton.Drive.App.Windows.Views.Main.SharedWithMe;

internal sealed class SharedWithMeViewModel : PageViewModel, ISharedWithMeOnboardingStateAware
{
    private readonly IOnboardingService _onboardingService;
    private bool _onboardingIsCompleted;

    public SharedWithMeViewModel(SharedWithMeListViewModel sharedWithMeList, IOnboardingService onboardingService)
    {
        SharedWithMeList = sharedWithMeList;
        _onboardingService = onboardingService;

        DismissOnboardingCommand = new RelayCommand(DismissOnboardingDialog);
    }

    public SharedWithMeListViewModel SharedWithMeList { get; }

    public bool OnboardingIsCompleted
    {
        get => _onboardingIsCompleted;
        private set => SetProperty(ref _onboardingIsCompleted, value);
    }

    public ICommand DismissOnboardingCommand { get; }

    void ISharedWithMeOnboardingStateAware.SharedWithMeOnboardingStateChanged(OnboardingStatus value)
    {
        OnboardingIsCompleted = value is OnboardingStatus.Completed;
    }

    internal override async void OnActivated()
    {
        base.OnActivated();

        await SharedWithMeList.LoadDataAsync().ConfigureAwait(true);
    }

    private void DismissOnboardingDialog()
    {
        _onboardingService.CompleteSharedWithMeOnboarding();
    }
}
