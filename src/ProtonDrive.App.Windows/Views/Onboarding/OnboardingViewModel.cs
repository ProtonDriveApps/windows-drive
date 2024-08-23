using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal sealed class OnboardingViewModel : ObservableObject
{
    private readonly List<OnboardingStepViewModel> _steps = new();
    private readonly CoalescingAction _updateCurrentStep;

    private OnboardingStepViewModel? _currentStep;

    public OnboardingViewModel(
        AccountRootFolderSelectionStepViewModel accountRootFolderSelectionStep,
        SyncFolderSelectionStepViewModel syncFolderSelectionStepViewModel,
        UpgradeStorageStepViewModel upgradeStorageStepViewModel)
    {
        AddStep(syncFolderSelectionStepViewModel);
        AddStep(accountRootFolderSelectionStep);
        AddStep(upgradeStorageStepViewModel);

        _updateCurrentStep = new CoalescingAction(UpdateCurrentStep);
        UpdateCurrentStep();
    }

    public OnboardingStepViewModel? CurrentStep
    {
        get => _currentStep;
        private set => SetProperty(ref _currentStep, value);
    }

    private void UpdateCurrentStep()
    {
        CurrentStep = _steps.FirstOrDefault(s => s.IsActive);
    }

    private void AddStep(OnboardingStepViewModel step)
    {
        _steps.Add(step);
        step.PropertyChanged += OnStepPropertyChanged;
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnboardingStepViewModel.IsActive))
        {
            _updateCurrentStep.Run();
        }
    }
}
