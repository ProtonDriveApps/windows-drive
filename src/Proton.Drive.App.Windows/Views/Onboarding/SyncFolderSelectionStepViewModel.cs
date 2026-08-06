using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Windows.Views.Main.MyComputer;
using Proton.Drive.Sdk.Sync.Agent.Mapping;

namespace Proton.Drive.App.Windows.Views.Onboarding;

internal sealed class SyncFolderSelectionStepViewModel : OnboardingStepViewModel
{
    private readonly IOnboardingService _onboardingService;

    private bool _isSaving;

    public SyncFolderSelectionStepViewModel(
        IOnboardingService onboardingService,
        AddFoldersViewModel addFoldersViewModel)
    {
        _onboardingService = onboardingService;

        AddFoldersViewModel = addFoldersViewModel;
        AddFoldersViewModel.PropertyChanged += OnFolderSelectionChanged;

        ContinueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue);
    }

    public AddFoldersViewModel AddFoldersViewModel { get; }

    public IAsyncRelayCommand ContinueCommand { get; }

    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    public override void Activate()
    {
        base.Activate();

        AddFoldersViewModel.InitializeSelection();
        ContinueCommand.NotifyCanExecuteChanged();
    }

    private void OnFolderSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AddFoldersViewModel && e.PropertyName == nameof(AddFoldersViewModel.FolderValidationResult))
        {
            ContinueCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanContinue()
    {
        return IsActive
               && AddFoldersViewModel.FolderValidationResult is SyncFolderValidationResult.Succeeded;
    }

    private async Task ContinueAsync()
    {
        IsSaving = true;

        try
        {
            await DelayBeforeSwitchingStepAsync().ConfigureAwait(true);

            await AddFoldersViewModel.SaveCommand.ExecuteAsync(null).ConfigureAwait(false);

            _onboardingService.CompleteStep(OnboardingStep.SyncFolderSelection);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
