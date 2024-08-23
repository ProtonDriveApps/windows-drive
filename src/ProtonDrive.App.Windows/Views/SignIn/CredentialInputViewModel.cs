using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Toolkit;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Client;

namespace ProtonDrive.App.Windows.Views.SignIn;

internal sealed class CredentialInputViewModel : SessionWorkflowStepWithPasswordViewModel, IDeferredValidationResolver
{
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly DispatcherScheduler _scheduler;
    private readonly AsyncRelayCommand _signInCommand;

    private string? _username;
    private bool _usernameFieldMustBeFocused = true;

    public CredentialInputViewModel(IAuthenticationService authenticationService, IExternalHyperlinks externalHyperlinks, DispatcherScheduler scheduler)
        : base(authenticationService)
    {
        _externalHyperlinks = externalHyperlinks;
        _scheduler = scheduler;

        _signInCommand = new AsyncRelayCommand(SignInAsync, CanSignIn);
        ResetPasswordCommand = new RelayCommand(ResetPassword);
        CreateAccountCommand = new RelayCommand(CreateAccount);
        OpenPrivacyPolicyCommand = new RelayCommand(OpenPrivacyPolicy);
        OpenTermsAndConditionsCommand = new RelayCommand(OpenTermsAndConditions);
    }

    public ICommand SignInCommand => _signInCommand;
    public ICommand ResetPasswordCommand { get; }
    public ICommand CreateAccountCommand { get; }
    public ICommand OpenPrivacyPolicyCommand { get; }
    public ICommand OpenTermsAndConditionsCommand { get; }

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
            : new ValidationResult($"{nameof(Username)} is required");
    }

    private ValidationResult? ValidatePassword()
    {
        ValidationResult? result;

        if (LastResponse is not null && LastResponse.Code != ResponseCode.Success)
        {
            result = new ValidationResult(LastResponse.Error ?? "Something went wrong");
        }
        else
        {
            result = (!FirstLoginAttempt && Password is null) || Password?.Length > 0
                ? ValidationResult.Success
                : new ValidationResult($"{nameof(Password)} is required");
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

    private void OpenPrivacyPolicy()
    {
        _externalHyperlinks.PrivacyPolicy.Open();
    }

    private void OpenTermsAndConditions()
    {
        _externalHyperlinks.TermsAndConditions.Open();
    }

    private bool CanSignIn()
    {
        return !string.IsNullOrEmpty(Username) && Password is not null && Password.Length > 0;
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
