using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.App.Windows.Views.SignIn;

internal abstract class SessionWorkflowStepViewModelBase : ObservableValidator
{
    private ApiResponse? _lastResponse;
    private bool _secretFieldMustBeFocused;

    protected SessionWorkflowStepViewModelBase(IAuthenticationService authenticationService)
    {
        AuthenticationService = authenticationService;
        RestartSignInCommand = new RelayCommand(RestartSignIn);
    }

    public ICommand RestartSignInCommand { get; }

    public ApiResponse? LastResponse
    {
        get => _lastResponse;
        set
        {
            _lastResponse = value;
            if (value != null)
            {
                ValidateAllProperties();
            }
        }
    }

    public bool SecretFieldMustBeFocused
    {
        get => _secretFieldMustBeFocused;
        protected set => SetProperty(ref _secretFieldMustBeFocused, value);
    }

    protected IAuthenticationService AuthenticationService { get; }

    private void RestartSignIn()
    {
        AuthenticationService.RestartAuthentication();
    }
}
