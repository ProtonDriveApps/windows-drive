using CommunityToolkit.Mvvm.ComponentModel;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Toolkit.Threading;
using Proton.Drive.Sdk.Sync.Shared.Authentication;

namespace Proton.Drive.App.Windows.Views.SignIn;

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

        _credentialInputViewModel = new CredentialInputViewModel(authenticationService, externalHyperlinks, scheduler);
        _secondFactorInputViewModel = new SecondFactorInputViewModel(authenticationService, externalHyperlinks, scheduler);
        _dataPasswordInputViewModel = new DataPasswordInputViewModel(authenticationService);

        _currentStepViewModel = _credentialInputViewModel;
    }

    string? IDialogViewModel.Title => null;

    public SessionWorkflowStepViewModelBase CurrentStepViewModel
    {
        get => _currentStepViewModel;
        private set
        {
            if (SetProperty(ref _currentStepViewModel, value))
            {
                OnPropertyChanged(nameof(IsSecondFactorView));
            }
        }
    }

    public bool IsSecondFactorView => CurrentStepViewModel is SecondFactorInputViewModel;

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => SetProperty(ref _isConnecting, value);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        if (value.Status is not (SessionStatus.SigningIn or SessionStatus.Starting))
        {
            ClearPasswords();
        }

        IsConnecting = value.SigningInStatus == SigningInStatus.Authenticating;

        if (value.Status is SessionStatus.Ending)
        {
            CurrentStepViewModel = _credentialInputViewModel;
            return;
        }

        if (value.Status is not SessionStatus.SigningIn)
        {
            return;
        }

        _secondFactorInputViewModel.HandleSessionStateChange(value);

        CurrentStepViewModel = value.SigningInStatus switch
        {
            SigningInStatus.WaitingForAuthenticationPassword => _credentialInputViewModel,
            SigningInStatus.WaitingForSecondFactorAuthentication => _secondFactorInputViewModel,
            SigningInStatus.WaitingForDataPassword => _dataPasswordInputViewModel,
            _ => CurrentStepViewModel,
        };

        CurrentStepViewModel.LastResponse = value.Response;
    }

    public void CancelAuthentication()
    {
        _authenticationService.CancelAuthenticationAsync();
        ClearPasswords();
    }

    private void ClearPasswords()
    {
        _credentialInputViewModel.Password = null;
        _credentialInputViewModel.FirstLoginAttempt = false;
        _secondFactorInputViewModel.TotpCode = null;
        _dataPasswordInputViewModel.Password = null;
    }
}
