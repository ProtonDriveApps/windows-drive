using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtonDrive.App.Configuration;
using ProtonDrive.App.Docs;
using ProtonDrive.App.InterProcessCommunication;
using ProtonDrive.App.Mapping.Teardown;
using ProtonDrive.App.Reporting;
using ProtonDrive.App.Update;
using ProtonDrive.App.Windows.Configuration;
using ProtonDrive.App.Windows.Interop;
using ProtonDrive.App.Windows.InterProcessCommunication;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit;
using ProtonDrive.App.Windows.Views.Main;
using ProtonDrive.App.Windows.Views.SystemTray;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Sync.Windows.FileSystem;

namespace ProtonDrive.App.Windows;

public static class Program
{
    private static IHost? _host;
    private static AppLaunchMode _appLaunchMode;
    private static bool _crashOnStartup;
    private static bool _crashOnMainWindowActivation;
    private static bool _uninstall;
    private static string? _documentPath;

    [STAThread]
    public static void Main(string[] args)
    {
        ParseArguments(args, out _appLaunchMode, out _documentPath, out _uninstall, out _crashOnStartup, out _crashOnMainWindowActivation);

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
    }

    private static void ParseArguments(
        string[] args,
        out AppLaunchMode launchMode,
        out string? documentPath,
        out bool uninstall,
        out bool crashOnStartup,
        out bool crashOnMainWindowActivation)
    {
        launchMode = args.Any(x => x.Equals("-quiet", StringComparison.OrdinalIgnoreCase))
            ? AppLaunchMode.Quiet
            : AppLaunchMode.Default;

        uninstall = args.Any(x => x.Equals("-uninstall", StringComparison.OrdinalIgnoreCase));

        documentPath = args.Take(1).FirstOrDefault(Path.IsPathFullyQualified);

        crashOnStartup = args.Any(x => x.Equals("-crashAndSendReport", StringComparison.OrdinalIgnoreCase));

        crashOnMainWindowActivation = args.Any(x => x.Equals("-crashLater", StringComparison.OrdinalIgnoreCase));
    }

    private static void RunApplication()
    {
        var errorReporting = ErrorReporting.Initialize(SentryOptionsProvider.GetOptions(() => _host?.Services));

        using var host = CreateHost(errorReporting);

        var updateService = host.Services.GetRequiredService<IUpdateService>();

        // Using synchronous call to stay on STA thread.
        // An asynchronous Main method does not respect the STA attribute, so it is not useful to propagate the asynchronicity up to it.
        if (TryInstallDownloadedUpdateAsync(updateService).GetAwaiter().GetResult())
        {
            return;
        }

        var app = host.Services.GetRequiredService<App>();
        app.InitializeComponent();

        AddAppEventHandlers(app, host, errorReporting);

        using var systemTrayControl = CreateSystemTrayControl(host);

        app.MainWindow = CreateMainWindow(host);

        ShowSystemTrayControl(app.MainWindow, systemTrayControl);

        app.Run();
    }

    private static void AddAppEventHandlers(Application app, IHost host, IErrorReporting errorReporting)
    {
        app.Startup += async (_, _) => await HandleAppStartupAsync(host, errorReporting).ConfigureAwait(true);
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

    private static async Task HandleAppStartupAsync(IHost host, IErrorReporting errorReporting)
    {
        host.Services.InitializeServices();
        await host.StartAsync().ConfigureAwait(true);

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

    private static IHost CreateHost(IErrorReporting errorReporting)
    {
        var host = Host.CreateDefaultBuilder()
            .AddAppConfiguration()
            .AddLogging()
            .AddApp(_appLaunchMode, _crashOnMainWindowActivation)
            .AddServices(errorReporting, _appLaunchMode)
            .Build();

        _host = host;

        Ioc.Default.ConfigureServices(host.Services);

        return host;
    }

    private static MainWindow CreateMainWindow(IHost host)
    {
        var mainViewModel = host.Services.GetRequiredService<MainWindowViewModel>();
        return new MainWindow
        {
            DataContext = mainViewModel,
            Visibility = _appLaunchMode == AppLaunchMode.Quiet ? Visibility.Collapsed : Visibility.Visible,
        };
    }

    private static void ShowSystemTrayControl(Window mainWindow, SystemTrayControl systemTrayControl)
    {
        new WindowInteropHelper(mainWindow).EnsureHandle();
        systemTrayControl.IsVisible = true;
    }

    private static void ThrowIfCrashOnStartupRequested(IErrorReporting errorReporting)
    {
        if (!_crashOnStartup)
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

    private static void OnUninstallingApp()
    {
        var localFolderStructureProtector = new SafeSyncFolderStructureProtectorDecorator(new NtfsPermissionsBasedSyncFolderStructureProtector());

        LocalMappedFoldersTeardownService.TryUnprotectLocalFolders(localFolderStructureProtector);

        SystemToastNotificationService.Uninstall();

        Win32ShellSyncFolderRegistry.Unregister();

        CloudFilterSyncRootRegistry.TryRemoveAllEntries();
    }
}
