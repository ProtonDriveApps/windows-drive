using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Toolkit.Behaviors;
using ProtonDrive.App.Windows.Views.Onboarding;
using ProtonDrive.App.Windows.Views.Shared;
using ProtonDrive.App.Windows.Views.Shared.Navigation;

namespace ProtonDrive.App.Windows.Views.Main;

internal sealed class MainWindowViewModel : ObservableObject, IVisibilityListener, ISessionStateAware, ICloseable
{
    private readonly OnboardingViewModel _onboarding;
    private readonly IStatefulSessionService _sessionService;
    private readonly MainViewModel _mainContent;

    private ObservableObject _content;
    private bool _isOnboarding;
    private bool _isDisplayed;

    public MainWindowViewModel(
        INavigationService<DetailsPageViewModel> detailsPages,
        OnboardingViewModel onboarding,
        MainViewModel mainContent,
        IStatefulSessionService sessionService)
    {
        DetailsPages = detailsPages;

        _onboarding = onboarding;
        _sessionService = sessionService;
        _content = _mainContent = mainContent;

        onboarding.PropertyChanged += OnOnboardingPropertyChanged;
        UpdateContent();
    }

    public INavigationService<DetailsPageViewModel> DetailsPages { get; }

    public ObservableObject Content
    {
        get => _content;
        private set
        {
            if (SetProperty(ref _content, value))
            {
                IsOnboarding = Content is OnboardingStepViewModel;
            }
        }
    }

    public bool IsOnboarding
    {
        get => _isOnboarding;
        private set
        {
            SetProperty(ref _isOnboarding, value);
        }
    }

    public void OnVisibilityChanged(bool isVisible)
    {
        if (!isVisible)
        {
            return;
        }

        _mainContent.CurrentMenuItem = ApplicationPage.Activity;
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        // For started session, the Sign-in window is displayed if the SigningInStatus has non default value
        _isDisplayed = value is
        {
            Status: SessionStatus.Started,
            SigningInStatus: SigningInStatus.None,
        };
    }

    void ICloseable.Close()
    {
        if (IsOnboarding && _isDisplayed)
        {
            _sessionService.EndSessionAsync();
        }
    }

    private void OnOnboardingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnboardingViewModel.CurrentStep))
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        Content = _onboarding.CurrentStep as ObservableObject ?? _mainContent;
    }
}
