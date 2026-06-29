using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Reporting;
using Proton.Drive.App.Windows.Views;
using Proton.Drive.App.Windows.Views.Onboarding;
using Proton.Drive.App.Windows.Views.SignIn;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Reporting;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows;

internal partial class App : IApp, ISessionStateAware, IOnboardingStateAware
{
    private readonly AppArguments _appArguments;
    private readonly IHost _host;
    private readonly IErrorReporting _errorReporting;
    private readonly AppLifecycleService _appLifecycleService;
    private readonly IScheduler _scheduler;
    private readonly ILogger<App> _logger;

    private Window? _signInWindow;
    private Window? _onboardingWindow;
    private bool _hasAttemptedToCrash;

    static App()
    {
        WpfLanguage.InitializeToCurrentCulture();
    }

    public App(
        AppArguments appArguments,
        IHost host,
        IErrorReporting errorReporting,
        AppLifecycleService appLifecycleService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler,
        ILogger<App> logger)
    {
        _appArguments = appArguments;
        _host = host;
        _errorReporting = errorReporting;
        _appLifecycleService = appLifecycleService;
        _scheduler = scheduler;
        _logger = logger;

        _appLifecycleService.StateChanged += OnAppLifecycleStateChanged;

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private App()
    {
        throw new NotSupportedException("This constructor only exists to satisfy WPF code generation");
    }

    public bool IsRestartRequested { get; private set; }

    public Window? GetActiveWindow()
    {
        return _signInWindow ?? _onboardingWindow ?? MainWindow;
    }

    public Task<IntPtr> ActivateAsync()
    {
        return Schedule(Activate);
    }

    public Task RestartAsync()
    {
        _logger.LogInformation("Restarting the app requested");
        IsRestartRequested = true;

        return ExitAsync();
    }

    public async Task ExitAsync()
    {
        // Shutdown can be called only from the thread that created the Application object.
        await Schedule(() =>
            {
                RemoveSignInWindow();
                RemoveOnboardingWindow();
                Shutdown();
            })
            .ConfigureAwait(true);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        Schedule(() => _appLifecycleService.SetSessionState(value));
    }

    void IOnboardingStateAware.OnboardingStateChanged(OnboardingState value)
    {
        Schedule(() => _appLifecycleService.SetOnboardingState(value));
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _logger.LogInformation("Windows user session is ending: {Reason}", e.ReasonSessionEnding);

        base.OnSessionEnding(e);
    }

    private static void CloseWindow(Window? window)
    {
        window?.Close();
    }

    private static IntPtr GetWindowHandle(Window? window)
    {
        return window != null ? new WindowInteropHelper(window).EnsureHandle() : IntPtr.Zero;
    }

    private IntPtr Activate()
    {
        _logger.LogInformation("Activating app");

        _appLifecycleService.Activate();

        var window = GetActiveWindow();

        return window is null ? IntPtr.Zero : GetWindowHandle(window);
    }

    private void OnAppLifecycleStateChanged(object? sender, AppLifecycleState appLifecycleState)
    {
        var window = appLifecycleState.CurrentWindow;

        _logger.LogInformation("Showing {Window} window", window);

        ShowWindow(window);
    }

    private void ShowWindow(AppWindow window)
    {
        switch (window)
        {
            case AppWindow.SignIn:
                CloseWindow(MainWindow);
                RemoveOnboardingWindow();
                ShowSignInWindow();
                break;

            case AppWindow.Onboarding:
                CloseWindow(MainWindow);
                RemoveSignInWindow();
                ShowOnboardingWindow();
                break;

            case AppWindow.Main:
                RemoveSignInWindow();
                RemoveOnboardingWindow();

                if (MainWindow is null)
                {
                    return;
                }

                ShowWindow(MainWindow);
                break;

            case AppWindow.None:
                RemoveSignInWindow();
                RemoveOnboardingWindow();
                CloseWindow(MainWindow);
                break;

            default:
                throw new InvalidEnumArgumentException(nameof(window), (int)window, typeof(AppWindow));
        }
    }

    private void ShowWindow(Window window)
    {
        window.Show();

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();

        if (_appArguments.CrashMode is AppCrashMode.OnMainWindowActivation && !_hasAttemptedToCrash && window == MainWindow)
        {
            _hasAttemptedToCrash = true;
            throw new IntentionalCrashException();
        }
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = (Exception)e.ExceptionObject;

        _logger.LogCritical(exception, "Unhandled AppDomain exception");

        // TODO: ShutdownGracefully() when we are confident that we won't have showstoppers
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled TaskScheduler exception");

        // TODO: Later Sentry versions should handle TaskScheduler UnobservedTask exceptions automatically
        _errorReporting.CaptureException(e.Exception);

        // TODO: ShutdownGracefully() when we are confident that we won't have showstoppers
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled Dispatcher exception");

        _errorReporting.CaptureException(e.Exception);
    }

    private void ShowSignInWindow()
    {
        _signInWindow ??= CreateSignInWindow();

        ShowWindow(_signInWindow);
    }

    private Window CreateSignInWindow()
    {
        var dialogViewModel = _host.Services.GetRequiredService<SessionWorkflowViewModel>();

        _signInWindow = new DialogWindow
        {
            DataContext = dialogViewModel,
        };

        _signInWindow.Closing += OnUserClosingSignInWindow;

        return _signInWindow;
    }

    private void ShowOnboardingWindow()
    {
        if (_onboardingWindow is null)
        {
            var viewModel = _host.Services.GetRequiredService<OnboardingViewModel>();

            _onboardingWindow = new OnboardingWindow
            {
                DataContext = viewModel,
            };
        }

        ShowWindow(_onboardingWindow);
    }

    private void RemoveSignInWindow(bool isClosing = false)
    {
        if (_signInWindow == null)
        {
            return;
        }

        _signInWindow.Closing -= OnUserClosingSignInWindow;

        if (!isClosing)
        {
            foreach (Window ownedWindow in _signInWindow.OwnedWindows)
            {
                ownedWindow.Close();
            }

            _signInWindow.Close();
        }

        _signInWindow = null;
    }

    private void RemoveOnboardingWindow()
    {
        _onboardingWindow?.Close();
        _onboardingWindow = null;
    }

    private void OnUserClosingSignInWindow(object? sender, CancelEventArgs e)
    {
        // ReSharper disable once LocalizableElement
        var signInWindow = sender as Window ?? throw new ArgumentException("This should not have happened", nameof(sender));

        if (_signInWindow != signInWindow)
        {
            return;
        }

        var sessionWorkflowViewModel = (SessionWorkflowViewModel)signInWindow.DataContext;
        sessionWorkflowViewModel.CancelAuthentication();

        RemoveSignInWindow(isClosing: true);
    }

    private Task Schedule(Action action)
    {
        return _scheduler.Schedule(action);
    }

    private Task<TResult> Schedule<TResult>(Func<TResult> function)
    {
        return _scheduler.Schedule(function);
    }
}
