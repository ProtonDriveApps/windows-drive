using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Toolkit.Threading;

namespace ProtonDrive.App.Windows.Views.SignIn;

internal sealed class SessionWorkflowViewModel : ObservableObject, ISessionStateAware, IDialogViewModel
{
    private readonly IAuthenticationService _authenticationService;
    private readonly CredentialInputViewModel _credentialInputViewModel;
    private readonly SecondFactorInputViewModel _secondFactorInputViewModel;
    private readonly DataPasswordInputViewModel _dataPasswordInputViewModel;

    private SessionWorkflowStepViewModelBase _currentStepViewModel;
    private bool _isConnecting;

    public SessionWorkflowViewModel(
        IAuthenticationService authenticationService,
        IExternalHyperlinks externalHyperlinks,
        DispatcherScheduler scheduler)
    {
        _authenticationService = authenticationService;

        _credentialInputViewModel = new(authenticationService, externalHyperlinks, scheduler);
        _secondFactorInputViewModel = new(authenticationService, scheduler);
        _dataPasswordInputViewModel = new(authenticationService);

        _currentStepViewModel = _credentialInputViewModel;
    }

    string? IDialogViewModel.Title => default;

    public SessionWorkflowStepViewModelBase CurrentStepViewModel
    {
        get => _currentStepViewModel;
        private set => SetProperty(ref _currentStepViewModel, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => SetProperty(ref _isConnecting, value);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        ClearPasswords();

        IsConnecting = value.SigningInStatus == SigningInStatus.Authenticating;

        if (value.Status != SessionStatus.SigningIn)
        {
            return;
        }

        CurrentStepViewModel = value.SigningInStatus switch
        {
            SigningInStatus.WaitingForAuthenticationPassword => _credentialInputViewModel,
            SigningInStatus.WaitingForSecondFactorCode => _secondFactorInputViewModel,
            SigningInStatus.WaitingForDataPassword => _dataPasswordInputViewModel,
            _ => CurrentStepViewModel,
        };

        CurrentStepViewModel.LastResponse = value.Response;
    }

    public void Cancel()
    {
        _authenticationService.CancelAuthenticationAsync();
        ClearPasswords();
    }

    private void ClearPasswords()
    {
        _credentialInputViewModel.Password = default;
        _credentialInputViewModel.FirstLoginAttempt = default;
        _secondFactorInputViewModel.Code = default;
        _dataPasswordInputViewModel.Password = default;
    }
}
