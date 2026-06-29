using System.Security;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.Extensions;
using Proton.Drive.App.Windows.Toolkit;
using Proton.Drive.Sdk.Sync.Shared.Authentication;

namespace Proton.Drive.App.Windows.Views.SignIn;

internal abstract class SessionWorkflowStepWithPasswordViewModel : SessionWorkflowStepViewModelBase
{
    private SecureString? _password;
    private string? _plainPassword;
    private bool _displayPlainPassword;
    private bool _requestPasswordFocus;

    protected SessionWorkflowStepWithPasswordViewModel(IAuthenticationService authenticationService)
        : base(authenticationService)
    {
        DisplayPlainPasswordCommand = new RelayCommand(() =>
        {
            DisplayPlainPassword = true;
            RequestPasswordFocus = false;
        });

        HidePlainPasswordCommand = new RelayCommand(() =>
        {
            DisplayPlainPassword = false;
            RequestPasswordFocus = true;
        });
    }

    [DeferredValidation]
    public SecureString? Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value, true))
            {
                OnPropertyChanged(nameof(IsPasswordEmpty));
                OnPasswordChanged();
            }
        }
    }

    public bool IsPasswordEmpty => Password is null || Password.Length == 0;

    public bool DisplayPlainPassword
    {
        get => _displayPlainPassword;
        private set
        {
            if (SetProperty(ref _displayPlainPassword, value))
            {
                PlainPassword = value && Password is not null ? Password.ConvertToUnsecureString() : default;
            }
        }
    }

    public string? PlainPassword
    {
        get => _plainPassword;
        private set => SetProperty(ref _plainPassword, value);
    }

    public bool RequestPasswordFocus
    {
        get => _requestPasswordFocus;
        private set => SetProperty(ref _requestPasswordFocus, value);
    }

    public ICommand DisplayPlainPasswordCommand { get; }
    public ICommand HidePlainPasswordCommand { get; }

    protected abstract void OnPasswordChanged();

    protected void HidePlainPassword()
    {
        DisplayPlainPassword = false;
    }
}
