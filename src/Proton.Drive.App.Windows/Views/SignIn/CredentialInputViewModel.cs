using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Toolkit;
using Proton.Drive.App.Windows.Toolkit.Threading;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.App.Windows.Views.SignIn;

internal sealed class CredentialInputViewModel : SessionWorkflowStepWithPasswordViewModel, IDeferredValidationResolver
{
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly DispatcherScheduler _scheduler;
    private readonly AsyncRelayCommand _signInCommand;

    private string? _username;
    private bool _usernameFieldMustBeFocused = true;

    public CredentialInputViewModel(
        IAuthenticationService authenticationService,
        IExternalHyperlinks externalHyperlinks,
        DispatcherScheduler scheduler)
        : base(authenticationService)
    {
        _externalHyperlinks = externalHyperlinks;
        _scheduler = scheduler;

        _signInCommand = new AsyncRelayCommand(SignInAsync, CanSignIn);
        ResetPasswordCommand = new RelayCommand(ResetPassword);
        CreateAccountCommand = new RelayCommand(CreateAccount);
    }

    public ICommand SignInCommand => _signInCommand;
    public ICommand ResetPasswordCommand { get; }
    public ICommand CreateAccountCommand { get; }

    public bool FirstLoginAttempt { get; set; }

    [DeferredValidation]
    public string? Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value, true))
            {
                _scheduler.Schedule(_signInCommand.NotifyCanExecuteChanged);
            }
        }
    }

    public bool UsernameFieldMustBeFocused
    {
        get => _usernameFieldMustBeFocused;
        set => SetProperty(ref _usernameFieldMustBeFocused, value);
    }

    ValidationResult? IDeferredValidationResolver.Validate(string? memberName)
    {
        return memberName switch
        {
            nameof(Username) => ValidateUsername(),
            nameof(Password) => ValidatePassword(),
            _ => ValidationResult.Success,
        };
    }

    protected override void OnPasswordChanged()
    {
        _scheduler.Schedule(_signInCommand.NotifyCanExecuteChanged);
    }

    private ValidationResult? ValidateUsername()
    {
        return (!FirstLoginAttempt && Username is null) || Username?.Length > 0
            ? ValidationResult.Success
            : new ValidationResult(Resources.Strings.SignIn_Text_ValidationError_UsernameRequired);
    }

    private ValidationResult? ValidatePassword()
    {
        ValidationResult? result;

        if (LastResponse is not null && LastResponse.Code != ResponseCode.Success)
        {
            result = new ValidationResult(
                LastResponse.Code switch
                {
                    ResponseCode.InvalidRefreshToken => Resources.Strings.SignIn_Text_ValidationError_SessionExpired,
                    _ => LastResponse.Error ?? Resources.Strings.SignIn_Text_ValidationError_SomethingWentWrong,
                });
        }
        else
        {
            result = (!FirstLoginAttempt && Password is null) || Password?.Length > 0
                ? ValidationResult.Success
                : new ValidationResult(Resources.Strings.SignIn_Text_ValidationError_PasswordRequired);
        }

        LastResponse = null;
        return result;
    }

    private void CreateAccount()
    {
        _externalHyperlinks.SignUp.Open();
    }

    private void ResetPassword()
    {
        _externalHyperlinks.ResetPassword.Open();
    }

    private bool CanSignIn()
    {
        return !string.IsNullOrEmpty(Username) && Password?.Length > 0;
    }

    private async Task SignInAsync()
    {
        HidePlainPassword();
        FirstLoginAttempt = true;

        ValidateProperty(Password, nameof(Password));
        ValidateProperty(Username, nameof(Username));

        if (string.IsNullOrEmpty(Username) || Password == null || Password.Length == 0)
        {
            return;
        }

        using var password = Password.Copy();
        password.MakeReadOnly();

        UsernameFieldMustBeFocused = false;

        await AuthenticationService.AuthenticateAsync(new NetworkCredential(Username, password)).ConfigureAwait(true);
    }
}
