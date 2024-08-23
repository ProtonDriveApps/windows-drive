using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Reporting;
using ProtonDrive.App.Windows.Dialogs;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.Views;
using ProtonDrive.App.Windows.Views.Main;
using ProtonDrive.App.Windows.Views.SignIn;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Windows;

internal partial class App : IApp, ISessionStateAware, IDialogService
{
    private readonly IHost _host;
    private readonly IErrorReporting _errorReporting;
    private readonly ILogger<App> _logger;
    private readonly AppLaunchMode _launchMode;

    private Window? _signInWindow;
    private SessionStatus? _previousSessionStatus;
    private bool _crashOnMainWindowActivation;

    static App()
    {
        WpfLanguage.InitializeToCurrentCulture();
    }

    private App()
    {
        throw new NotSupportedException("This constructor only exists to satisfy WPF code generation");
    }

    public App(IHost host, IErrorReporting errorReporting, ILogger<App> logger, AppLaunchMode launchMode, bool crashOnMainWindowActivation)
    {
        _host = host;
        _errorReporting = errorReporting;
        _logger = logger;
        _launchMode = launchMode;
        _crashOnMainWindowActivation = crashOnMainWindowActivation;

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    public Task<IntPtr> ActivateAsync()
    {
        return Dispatcher.InvokeAsync(() =>
            {
                if (IsUserSignedOut())
                {
                    _host.Services.GetRequiredService<IStatefulSessionService>().StartSessionAsync();

                    // The Sign in window will be created as a reaction to the session state change.
                    // Therefore, as the Sign in window does not exist yet, we return IntPtr.Zero.
                    return GetWindowHandle(_signInWindow);
                }

                var window = _signInWindow?.Visibility == Visibility.Visible ? _signInWindow : MainWindow;
                ShowWindow(window);

                return GetWindowHandle(window);
            })
            .Task;
    }

    public ConfirmationResult ShowConfirmationDialog(ConfirmationDialogViewModelBase dataContext)
    {
        var confirmationDialog = new ConfirmationDialogWindow
        {
            DataContext = dataContext,
            Owner = MainWindow,
        };

        var result = confirmationDialog.ShowDialog();

        if (result is null)
        {
            return ConfirmationResult.Cancelled;
        }

        return result.GetValueOrDefault() ? ConfirmationResult.Confirmed : ConfirmationResult.Cancelled;
    }

    public void Show(IDialogViewModel dataContext)
    {
        var dialog = new DialogWindow
        {
            DataContext = dataContext,
            Owner = MainWindow,
        };

        dialog.Show();
    }

    public void ShowDialog(IDialogViewModel dataContext)
    {
        var dialog = new DialogWindow
        {
            DataContext = dataContext,
            Owner = MainWindow,
        };

        dialog.ShowDialog();
    }

    public async Task ExitAsync()
    {
        // Shutdown can be called only from the thread that created the Application object.
        await Dispatcher.InvokeAsync(Shutdown);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (value.Status)
            {
                case SessionStatus.SigningIn:
                    CloseWindow(MainWindow);

                    if (value.IsFirstSessionStart && _launchMode == AppLaunchMode.Quiet)
                    {
                        CancelSignIn();
                    }
                    else
                    {
                        CreateAndShowSignInWindow();
                    }

                    break;

                case SessionStatus.Started:
                    if (value.SigningInStatus is not SigningInStatus.None)
                    {
                        break;
                    }

                    CloseSignInWindow();

                    if (UserSignedInInteractively() || UserIsBeingOnboarded() || SessionStartIsFirstInNonQuietMode())
                    {
                        ShowWindow(MainWindow);
                    }

                    break;

                case SessionStatus.NotStarted:
                    CloseWindow(MainWindow);
                    break;

                default:
                    CloseSignInWindow();
                    break;
            }

            _previousSessionStatus = value.Status;
        });

        return;

        bool UserSignedInInteractively() => _previousSessionStatus is SessionStatus.SigningIn or SessionStatus.Started;
        bool UserIsBeingOnboarded() => MainWindow?.DataContext is MainWindowViewModel { IsOnboarding: true };
        bool SessionStartIsFirstInNonQuietMode() => value.IsFirstSessionStart && _launchMode != AppLaunchMode.Quiet;
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

    private void ShowWindow(Window? window)
    {
        if (window == null)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();

        if (_crashOnMainWindowActivation && window == MainWindow)
        {
            _crashOnMainWindowActivation = false;
            throw new IntentionalCrashException();
        }
    }

    private void CancelSignIn()
    {
        var session = _host.Services.GetRequiredService<SessionWorkflowViewModel>();
        session.Cancel();
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

    private void CreateAndShowSignInWindow()
    {
        if (_signInWindow != null)
        {
            ShowWindow(_signInWindow);
            return;
        }

        var dialogViewModel = _host.Services.GetRequiredService<SessionWorkflowViewModel>();

        var signInWindow = new DialogWindow
        {
            DataContext = dialogViewModel,
        };

        _signInWindow = signInWindow;
        signInWindow.Closing += OnSignInWindowClosing;
        signInWindow.Show();
    }

    private void CloseSignInWindow()
    {
        var signInWindow = _signInWindow;

        if (signInWindow == null)
        {
            return;
        }

        _signInWindow = null;
        signInWindow.Closing -= OnSignInWindowClosing;
        signInWindow.Close();
    }

    private void OnSignInWindowClosing(object? sender, CancelEventArgs e)
    {
        // ReSharper disable once LocalizableElement
        var signInWindow = sender as Window ?? throw new ArgumentException("This should not have happened", nameof(sender));
        signInWindow.Closing -= OnSignInWindowClosing;

        if (_signInWindow != signInWindow)
        {
            return;
        }

        var sessionWorkflowViewModel = (SessionWorkflowViewModel)signInWindow.DataContext;
        sessionWorkflowViewModel.Cancel();

        _signInWindow = null;
    }

    private bool IsUserSignedOut()
    {
        return (_previousSessionStatus is SessionStatus.NotStarted or SessionStatus.Ending)
               || (_previousSessionStatus is SessionStatus.SigningIn && _signInWindow == null);
    }
}
