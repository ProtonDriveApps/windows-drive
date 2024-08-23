using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Toolkit;
using ProtonDrive.Client;

namespace ProtonDrive.App.Windows.Views.SignIn;

internal sealed class DataPasswordInputViewModel : SessionWorkflowStepWithPasswordViewModel, IDeferredValidationResolver
{
    public DataPasswordInputViewModel(IAuthenticationService authenticationService)
        : base(authenticationService)
    {
        ContinueSigningInCommand = new AsyncRelayCommand(ContinueSigningInAsync);
        SecretFieldMustBeFocused = true;
    }

    protected override void OnPasswordChanged()
    {
        // Nothing to do
    }

    public ICommand ContinueSigningInCommand { get; }

    ValidationResult? IDeferredValidationResolver.Validate(string? memberName)
    {
        switch (memberName)
        {
            case nameof(Password):
                var result = LastResponse is not null && LastResponse.Code != ResponseCode.Success
                    ? new ValidationResult(LastResponse.Error ?? "Something went wrong")
                    : ValidationResult.Success;
                LastResponse = null;
                return result;

            default:
                return ValidationResult.Success;
        }
    }

    private async Task ContinueSigningInAsync()
    {
        HidePlainPassword();

        // TODO: Validate and check if valid
        if (Password == null || Password.Length == 0)
        {
            return;
        }

        using var password = Password.Copy();
        password.MakeReadOnly();

        await AuthenticationService.FinishTwoPasswordAuthenticationAsync(password).ConfigureAwait(false);
    }
}
