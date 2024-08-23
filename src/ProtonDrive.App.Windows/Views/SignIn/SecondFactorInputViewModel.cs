using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Toolkit;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Client;

namespace ProtonDrive.App.Windows.Views.SignIn;

internal sealed class SecondFactorInputViewModel : SessionWorkflowStepViewModelBase, IDeferredValidationResolver
{
    private readonly AsyncRelayCommand _continueSigningInCommand;
    private readonly DispatcherScheduler _scheduler;
    private string? _code;

    public SecondFactorInputViewModel(IAuthenticationService authenticationService, DispatcherScheduler scheduler)
        : base(authenticationService)
    {
        _scheduler = scheduler;
        _continueSigningInCommand = new AsyncRelayCommand(ContinueSigningInAsync, CanContinueSigningIn);
        SecretFieldMustBeFocused = true;
    }

    public ICommand ContinueSigningInCommand => _continueSigningInCommand;

    [DeferredValidation]
    public string? Code
    {
        get => _code;
        set
        {
            if (SetProperty(ref _code, value, true))
            {
                _scheduler.Schedule(() => _continueSigningInCommand.NotifyCanExecuteChanged());
            }
        }
    }

    ValidationResult? IDeferredValidationResolver.Validate(string? memberName)
    {
        switch (memberName)
        {
            case nameof(Code):
                var result = LastResponse is not null && LastResponse.Code != ResponseCode.Success
                    ? new ValidationResult(LastResponse.Error ?? "Incorrect code")
                    : ValidationResult.Success;
                LastResponse = null;
                return result;

            default:
                return ValidationResult.Success;
        }
    }

    private bool CanContinueSigningIn()
    {
        return !string.IsNullOrEmpty(Code);
    }

    private async Task ContinueSigningInAsync()
    {
        if (string.IsNullOrEmpty(Code))
        {
            return;
        }

        await AuthenticationService.FinishTwoFactorAuthenticationAsync(Code).ConfigureAwait(true);
    }
}
