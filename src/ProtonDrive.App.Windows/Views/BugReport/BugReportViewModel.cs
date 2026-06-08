using System.Globalization;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Reporting;
using ProtonDrive.App.Windows.Views.Shared;
using ProtonDrive.Client.BugReport;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.App.Windows.Views.BugReport;

internal sealed class BugReportViewModel : ObservableObject, ICloseable, IDialogViewModel
{
    private readonly AppConfig _config;
    private readonly IBugReportService _bugReportService;
    private readonly ILocalFolderService _localFolderService;
    private readonly ILogger<BugReportViewModel> _logger;
    private readonly IAsyncRelayCommand _reportBugCommand;
    private readonly IAsyncRelayCommand _openLogsFolderCommand;
    private readonly string? _username;

    private string? _emailAddress;
    private bool _includeLogs;

    public BugReportViewModel(
        AppConfig config,
        AppStateViewModel appState,
        IBugReportService bugReportService,
        ILocalFolderService localFolderService,
        ILogger<BugReportViewModel> logger)
    {
        _config = config;
        _bugReportService = bugReportService;
        _localFolderService = localFolderService;
        _logger = logger;

        _reportBugCommand = new AsyncRelayCommand(ReportBugAsync, CanReportBug);
        _openLogsFolderCommand = new AsyncRelayCommand(OpenLogsFolderAsync);

        _includeLogs = true;
        _emailAddress = appState.User?.EmailAddress;
        _username = appState.User?.Name;
    }

    string IDialogViewModel.Title => "Report a problem";

    public string? EmailAddress
    {
        get => _emailAddress;
        set
        {
            if (SetProperty(ref _emailAddress, value))
            {
                _reportBugCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? Description
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _reportBugCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? Title
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _reportBugCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IncludeLogs
    {
        get => _includeLogs;
        set => SetProperty(ref _includeLogs, value);
    }

    public bool ReportSuccessfullySent
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string? ErrorMessage
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsBusy
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _reportBugCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ICommand ReportBugCommand => _reportBugCommand;

    public ICommand OpenLogsFolderCommand => _openLogsFolderCommand;

    public void Close()
    {
        _reportBugCommand.Cancel();
    }

    private void ClearErrorMessage()
    {
        ErrorMessage = null;
    }

    private async Task OpenLogsFolderAsync()
    {
        ClearErrorMessage();

        var logsFolderPath = Path.Combine(_config.AppDataPath, "Logs");

        var result = await _localFolderService.OpenFolderAsync(logsFolderPath).ConfigureAwait(true);

        if (!result)
        {
            ErrorMessage = "Logs folder cannot be opened.";
        }
    }

    private bool CanReportBug()
    {
        return !IsBusy && EmailAddress?.Length > 0 && Title?.Length > 0 && Description?.Length > 10;
    }

    private async Task ReportBugAsync(CancellationToken cancellationToken)
    {
        ClearErrorMessage();

        var parameters = new BugReportBody
        {
            Os = "Windows",
            OsVersion = Environment.OSVersion.VersionString,
            Username = _username,
            Title = Title ?? throw new InvalidOperationException("Title is missing"),
            EmailAddress = EmailAddress ?? throw new InvalidOperationException("E-mail address is missing"),
            Description = Description ?? throw new InvalidOperationException("Description is missing"),
            Client = "Windows Drive",
            ClientVersion = _config.AppVersion.ToString(),
            ClientType = ((int)BugReportClientType.Drive).ToString(CultureInfo.InvariantCulture),
        };

        try
        {
            IsBusy = true;

            var result = await _bugReportService.SendAsync(parameters, IncludeLogs, cancellationToken).ConfigureAwait(true);

            ReportSuccessfullySent = result.IsSuccess;
            ErrorMessage = result.ErrorMessage;
        }
        catch (OperationCanceledException)
        {
            // Expected
            _logger.LogInformation("Sending bug report was cancelled");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
