using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Toolkit;
using Proton.Drive.App.Windows.Toolkit.Threading;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.App.Windows.Views.SignIn;

internal sealed class SecondFactorInputViewModel : SessionWorkflowStepViewModelBase, IDeferredValidationResolver
{
    private static readonly Dictionary<MultiFactorAuthenticationMethods, SecondFactorInputPage> AuthenticationMethodToInputPageMapping = new()
        {
            { MultiFactorAuthenticationMethods.Totp, SecondFactorInputPage.Totp },
            { MultiFactorAuthenticationMethods.Fido2, SecondFactorInputPage.Fido2 },
        };

    private readonly AsyncRelayCommand _continueSigningInCommand;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly DispatcherScheduler _scheduler;

    private SecondFactorInputPage _currentPage;
    private bool _isFido2Available;
    private IReadOnlyCollection<SecondFactorInputPage> _enabledPages = [];
    private bool _hasMultipleEnabledPages;
    private string? _totpCode;
    private string? _lastAttemptedTotpCode;
    private bool _requestTotpCodeFocus;

    public SecondFactorInputViewModel(
        IAuthenticationService authenticationService,
        IExternalHyperlinks externalHyperlinks,
        DispatcherScheduler scheduler)
        : base(authenticationService)
    {
        _externalHyperlinks = externalHyperlinks;
        _scheduler = scheduler;

        _continueSigningInCommand = new AsyncRelayCommand(ContinueSigningInAsync, CanContinueSigningIn);
        LearnMoreCommand = new RelayCommand(LearnMore);
        SecretFieldMustBeFocused = true;
    }

    public ICommand ContinueSigningInCommand => _continueSigningInCommand;
    public ICommand LearnMoreCommand { get; }

    public bool RequestTotpCodeFocus
    {
        get => _requestTotpCodeFocus;
        private set => SetProperty(ref _requestTotpCodeFocus, value);
    }

    public SecondFactorInputPage CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                TotpCode = null;
                _scheduler.Schedule(() => _continueSigningInCommand.NotifyCanExecuteChanged());
                RequestTotpCodeFocus = value == SecondFactorInputPage.Totp;
            }
        }
    }

    public IReadOnlyCollection<SecondFactorInputPage> EnabledPages
    {
        get => _enabledPages;
        private set
        {
            if (SetProperty(ref _enabledPages, value))
            {
                HasMultipleEnabledPages = value.Count > 1;
            }
        }
    }

    public bool HasMultipleEnabledPages
    {
        get => _hasMultipleEnabledPages;
        private set => SetProperty(ref _hasMultipleEnabledPages, value);
    }

    public bool IsFido2Available
    {
        get => _isFido2Available;
        private set => SetProperty(ref _isFido2Available, value);
    }

    [DeferredValidation]
    public string? TotpCode
    {
        get => _totpCode;
        set
        {
            if (SetProperty(ref _totpCode, value, true))
            {
                _scheduler.Schedule(() => _continueSigningInCommand.NotifyCanExecuteChanged());
            }
        }
    }

    ValidationResult? IDeferredValidationResolver.Validate(string? memberName)
    {
        switch (memberName)
        {
            case nameof(TotpCode):
                var result = LastResponse is not null && LastResponse.Code != ResponseCode.Success
                    ? new ValidationResult(LastResponse.Error ?? "Incorrect code")
                    : ValidationResult.Success;
                LastResponse = null;
                return result;

            default:
                return ValidationResult.Success;
        }
    }

    public void HandleSessionStateChange(SessionState sessionState)
    {
        if (sessionState.SigningInStatus is not SigningInStatus.WaitingForSecondFactorAuthentication)
        {
            return;
        }

        // Clear 2FA code only on success; otherwise keep it to show validation errors.
        if (sessionState.Response.Succeeded)
        {
            TotpCode = null;
        }

        EnabledPages =
            AuthenticationMethodToInputPageMapping
                .Where(mapping => sessionState.MultiFactorAuthenticationMethods.HasFlag(mapping.Key))
                .Select(mapping => mapping.Value)
                .Distinct()
                .ToList();

        // We keep the current page if it is enabled.
        // We do not want FIDO2 to be current when not available, unless it is the only choice.
        // If no pages are enabled, we still show the default page (NotAvailable).
        CurrentPage =
            EnabledPages.Where(page => page == CurrentPage)
                .Concat(EnabledPages.Where(page => page is not SecondFactorInputPage.Fido2 || sessionState.IsFido2Available))
                .Append(EnabledPages.FirstOrDefault(page => page is SecondFactorInputPage.Fido2))
                .FirstOrDefault();

        IsFido2Available = sessionState.IsFido2Available;
    }

    private bool CanContinueSigningIn()
    {
        return CurrentPage switch
        {
            SecondFactorInputPage.Totp => TotpCodeIsValid(),
            SecondFactorInputPage.Fido2 => true,
            SecondFactorInputPage.NotSupported => false,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private async Task ContinueSigningInAsync()
    {
        switch (CurrentPage)
        {
            case SecondFactorInputPage.Totp:
                if (!TotpCodeIsValid())
                {
                    return;
                }

                await AuthenticationService.AuthenticateWithTotpAsync(TotpCode).ConfigureAwait(true);
                _lastAttemptedTotpCode = TotpCode;
                await _scheduler.ScheduleAsync(() => _continueSigningInCommand.NotifyCanExecuteChanged()).ConfigureAwait(true);
                break;

            case SecondFactorInputPage.Fido2:
                await AuthenticationService.AuthenticateWithFido2Async().ConfigureAwait(true);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [MemberNotNullWhen(true, nameof(TotpCode))]
    private bool TotpCodeIsValid()
    {
        return TotpCode is { Length: 6 } && !TotpCode.Equals(_lastAttemptedTotpCode, StringComparison.OrdinalIgnoreCase);
    }

    private void LearnMore()
    {
        _externalHyperlinks.HowToUseSecurityKey.Open();
    }
}
