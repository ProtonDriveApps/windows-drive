using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Proton.Drive.App.Configuration;
using Proton.Drive.App.Docs;
using Proton.Drive.App.InterProcessCommunication;
using Proton.Drive.App.Reporting;
using Proton.Drive.App.Update;
using Proton.Drive.App.Windows.Configuration;
using Proton.Drive.App.Windows.Interop;
using Proton.Drive.App.Windows.InterProcessCommunication;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.App.Windows.Toolkit;
using Proton.Drive.App.Windows.Views.Main;
using Proton.Drive.App.Windows.Views.SystemTray;
using Proton.Drive.Sdk.Sync.Agent.Configuration;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Teardown;
using Proton.Drive.Sdk.Sync.Windows.FileSystem;
using Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Windows.FileSystem.OnDemand;
using Proton.Drive.Sdk.Sync.Windows.Shell;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Localization;
using Proton.Drive.Shared.Reporting;

namespace Proton.Drive.App.Windows;

public static class Program
{
    private static AppLaunchMode _appLaunchMode;
    private static AppCrashMode _appCrashMode;
    private static bool _uninstall;
    private static string? _documentPath;
    private static bool _isAppRestartRequested;

    [STAThread]
    public static void Main(string[] args)
    {
        ParseArguments(args, out _appLaunchMode, out _documentPath, out _uninstall, out _appCrashMode);

        var appConfig = new AppConfig();

        Shell32.SetCurrentProcessExplicitAppUserModelID(appConfig.ApplicationId);

        FileSystemObject.ExposePlaceholders();

        if (_uninstall)
        {
            OnUninstallingApp();
            return;
        }

        var otherProcessExists = !SingletonProcessInvoker.TryInvoke(RunApplication);
        if (otherProcessExists)
        {
            if (_documentPath is not null)
            {
                OpenDocumentFromOtherProcessAsync(_documentPath, CancellationToken.None).GetAwaiter().GetResult();
                return;
            }

            if (_appLaunchMode != AppLaunchMode.Quiet)
            {
                AppActivator.ActivateExistingProcessWindow();
            }
        }
        else
        {
            RestartAppIfRequested();
        }
    }

    private static void ParseArguments(
        string[] args,
        out AppLaunchMode launchMode,
        out string? documentPath,
        out bool uninstall,
        out AppCrashMode crashMode)
    {
        launchMode = args.Any(x => x.Equals("-quiet", StringComparison.OrdinalIgnoreCase))
            ? AppLaunchMode.Quiet
            : AppLaunchMode.Default;

        uninstall = args.Any(x => x.Equals("-uninstall", StringComparison.OrdinalIgnoreCase));

        documentPath = args.Take(1).FirstOrDefault(Path.IsPathFullyQualified);

        crashMode = AppCrashMode.None;
        crashMode = args.Any(x => x.Equals("-crashLater", StringComparison.OrdinalIgnoreCase)) ? AppCrashMode.OnMainWindowActivation : crashMode;
        crashMode = args.Any(x => x.Equals("-crashAndSendReport", StringComparison.OrdinalIgnoreCase)) ? AppCrashMode.OnStartup : crashMode;
    }

    private static void RunApplication()
    {
        using var host = CreateHost();

        host.Services.GetRequiredService<AppLifecycleLogger>().LogAppStart();

        var updateService = host.Services.GetRequiredService<IUpdateService>();

        // Using synchronous call to stay on STA thread.
        // An asynchronous Main method does not respect the STA attribute, so it is not useful to propagate the asynchronicity up to it.
        if (TryInstallDownloadedUpdateAsync(updateService).GetAwaiter().GetResult())
        {
            return;
        }

        var currentLocale = CultureInfo.CurrentUICulture.Name;
        var languageProvider = host.Services.GetRequiredService<ILanguageProvider>();
        var culture = languageProvider.GetCulture();

        if (!currentLocale.Equals(culture, StringComparison.OrdinalIgnoreCase))
        {
            Resources.Strings.Culture = new CultureInfo(culture);
        }

        var app = host.Services.GetRequiredService<App>();

        app.InitializeComponent();

        AddAppEventHandlers(app, host);

        using var systemTrayControl = CreateSystemTrayControl(host);

        app.MainWindow = CreateMainWindow(host);

        ShowSystemTrayControl(app.MainWindow, systemTrayControl);

        app.Run();

        host.Services.GetRequiredService<AppLifecycleLogger>().LogAppExit();

        _isAppRestartRequested = app.IsRestartRequested;
    }

    private static void AddAppEventHandlers(Application app, IHost host)
    {
        app.Startup += async (_, _) => await HandleAppStartupAsync(host).ConfigureAwait(true);
        app.Exit += (_, _) => HandleAppExit(host);
    }

    private static void HandleAppExit(IHost host)
    {
        const int secondsToStopGracefully = 10;
        var stopTask = host.StopAsync(TimeSpan.FromSeconds(secondsToStopGracefully));
        WaitWithNewDispatcherFrame(stopTask);
        return;

        // TODO: avoid this hack. That is used to avoid a deadlock due to some services being dependent on the main UI thread.
        static void WaitWithNewDispatcherFrame(Task task)
        {
            var nestedFrame = new DispatcherFrame();

            task.ContinueWith(_ => nestedFrame.Continue = false);

            Dispatcher.PushFrame(nestedFrame);

            task.Wait();
        }
    }

    private static async Task HandleAppStartupAsync(IHost host)
    {
        host.Services.InitializeServices();
        await host.StartAsync().ConfigureAwait(true);

        var errorReporting = host.Services.GetRequiredService<IErrorReporting>();
        ThrowIfCrashOnStartupRequested(errorReporting);

        await OpenDocumentIfRequestedAsync(host, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task OpenDocumentIfRequestedAsync(IHost host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_documentPath))
        {
            return;
        }

        var documentOpener = host.Services.GetRequiredService<DocumentOpener>();
        await documentOpener.TryOpenAsync(_documentPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task OpenDocumentFromOtherProcessAsync(string documentPath, CancellationToken cancellationToken)
    {
        var ipcClient = await NamedPipeBasedIpcClient.ConnectAsync(NamedPipeBasedIpcServer.PipeName, TimeSpan.FromSeconds(1), cancellationToken)
            .ConfigureAwait(false);

        await using (ipcClient.ConfigureAwait(false))
        {
            await ipcClient.WriteAsync(IpcMessageType.OpenDocumentCommand, documentPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SystemTrayControl CreateSystemTrayControl(IHost host)
    {
        SystemTrayControl? systemTrayControl = null;
        try
        {
            var systemTrayViewModel = host.Services.GetRequiredService<SystemTrayViewModel>();
            return new SystemTrayControl(systemTrayViewModel);
        }
        catch
        {
            systemTrayControl?.Dispose();
            throw;
        }
    }

    private static IHost CreateHost()
    {
        var host = Host.CreateDefaultBuilder()
            .AddAppConfiguration()
            .AddLogging()
            .AddApp(new AppArguments(_appLaunchMode, _appCrashMode))
            .AddServices()
            .Build();

        return host;
    }

    private static MainWindow CreateMainWindow(IHost host)
    {
        var mainViewModel = host.Services.GetRequiredService<MainWindowViewModel>();

        return new MainWindow
        {
            DataContext = mainViewModel,
            Visibility = Visibility.Collapsed,
        };
    }

    private static void ShowSystemTrayControl(Window mainWindow, SystemTrayControl systemTrayControl)
    {
        new WindowInteropHelper(mainWindow).EnsureHandle();
        systemTrayControl.IsVisible = true;
    }

    private static void ThrowIfCrashOnStartupRequested(IErrorReporting errorReporting)
    {
        if (_appCrashMode is not AppCrashMode.OnStartup)
        {
            return;
        }

        errorReporting.IsEnabled = true;
        throw new IntentionalCrashException();
    }

    private static Task<bool> TryInstallDownloadedUpdateAsync(IUpdateService updateService)
    {
        return updateService.TryInstallDownloadedUpdateAsync();
    }

    private static void RestartAppIfRequested()
    {
        if (!_isAppRestartRequested)
        {
            return;
        }

        var exePath = Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        Process.Start(exePath);
    }

    private static void OnUninstallingApp()
    {
        var localFolderStructureProtector =
            new SafeSyncFolderStructureProtectorDecorator(
                new NtfsPermissionsBasedSyncFolderStructureProtector(
                    NullLogger<NtfsPermissionsBasedSyncFolderStructureProtector>.Instance));

        var placeholderConverter = new PlaceholderToRegularItemConverter(NullLogger<PlaceholderToRegularItemConverter>.Instance);
        var readOnlyFileAttributeRemover = new ReadOnlyFileAttributeRemover(NullLogger<ReadOnlyFileAttributeRemover>.Instance);

        LocalMappedFoldersTeardownService.TryTearDownLocalFolders(localFolderStructureProtector, placeholderConverter, readOnlyFileAttributeRemover);

        SystemToastNotificationService.Uninstall();

        Win32ShellSyncFolderRegistry.Unregister();

        CloudFilterSyncRootRegistry.TryRemoveAllEntries();
    }
}
