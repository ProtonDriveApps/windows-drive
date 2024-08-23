using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.App.Windows.Views.Main.Computers;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal sealed class SyncFolderSelectionStepViewModel : OnboardingStepViewModel
{
    private readonly IOnboardingService _onboardingService;

    private bool _isSaving;

    public SyncFolderSelectionStepViewModel(
        IOnboardingService onboardingService,
        DispatcherScheduler scheduler,
        AddFoldersViewModel addFoldersViewModel)
    : base(scheduler)
    {
        _onboardingService = onboardingService;

        AddFoldersViewModel = addFoldersViewModel;
        AddFoldersViewModel.PropertyChanged += OnFolderSelectionChanged;

        ContinueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue);
        ContinueCommand.PropertyChanged += OnAsyncRelayCommandPropertyChanged;
    }

    public AddFoldersViewModel AddFoldersViewModel { get; }

    public IAsyncRelayCommand ContinueCommand { get; }

    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    protected override void Activate()
    {
        AddFoldersViewModel.InitializeSelection();
        IsActive = true;
        IsSaving = false;
        ContinueCommand.NotifyCanExecuteChanged();
    }

    protected override bool SkipActivation()
    {
        return _onboardingService.IsSyncFolderSelectionCompleted();
    }

    private static void OnAsyncRelayCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AsyncRelayCommand command && e.PropertyName == nameof(AsyncRelayCommand.IsRunning))
        {
            command.NotifyCanExecuteChanged();
        }
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
               && !ContinueCommand.IsRunning
               && AddFoldersViewModel.FolderValidationResult is SyncFolderValidationResult.Succeeded;
    }

    private async Task ContinueAsync()
    {
        IsSaving = true;

        AddFoldersViewModel.SaveCommand.Execute(default);

        _onboardingService.SetSyncFolderSelectionCompleted();

        await Task.Delay(DelayBeforeSwitchingStep).ConfigureAwait(true);

        IsActive = false;
    }
}
