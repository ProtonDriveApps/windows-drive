using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Update;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Update;
using ICommand = System.Windows.Input.ICommand;

namespace ProtonDrive.App.Windows.Views.Main.About;

internal sealed class AboutViewModel : PageViewModel
{
    private const string DefaultCopyrightMessage = "© Proton AG";

    private readonly IApp _app;
    private readonly IUpdateService _updateService;
    private readonly DispatcherScheduler _scheduler;
    private readonly IExternalHyperlinks _externalHyperlinks;

    private readonly RelayCommand _updateCommand;

    private DateTime? _releaseDate;
    private UpdateStatus _updateStatus;
    private Version _newVersion = new();
    private IReadOnlyList<ReleaseNoteViewModel> _releaseNotes = Array.Empty<ReleaseNoteViewModel>();

    public AboutViewModel(
        AppConfig config,
        IApp app,
        IUpdateService updateService,
        DispatcherScheduler scheduler,
        IExternalHyperlinks externalHyperlinks)
    {
        _app = app;
        _updateService = updateService;
        _scheduler = scheduler;
        _externalHyperlinks = externalHyperlinks;

        OpenPrivacyPolicyCommand = new RelayCommand(OpenPrivacyPolicy);
        OpenTermsAndConditionsCommand = new RelayCommand(OpenTermsAndConditions);

        updateService.StateChanged += OnUpdateServiceStateChanged;

        CurrentVersion = config.AppVersion;

        _updateCommand = new RelayCommand(Update, CanUpdate);
    }

    public ICommand UpdateCommand => _updateCommand;

    public ICommand OpenPrivacyPolicyCommand { get; }

    public ICommand OpenTermsAndConditionsCommand { get; }

    public Version CurrentVersion { get; }

    public IReadOnlyList<ReleaseNoteViewModel> ReleaseNotes
    {
        get => _releaseNotes;
        private set => SetProperty(ref _releaseNotes, value);
    }

    public string CopyrightMessage
    {
        get
        {
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            var copyright = attributes.Length == 0 ? DefaultCopyrightMessage : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
#if DEBUG
            copyright = copyright.Replace("{Year}", DateTime.Today.Year.ToString());
#endif
            return copyright;
        }
    }

    public DateTime? ReleaseDate
    {
        get => _releaseDate;
        private set => SetProperty(ref _releaseDate, value);
    }

    public UpdateStatus UpdateStatus
    {
        get => _updateStatus;
        private set
        {
            if (SetProperty(ref _updateStatus, value))
            {
                _scheduler.Schedule(() => _updateCommand.NotifyCanExecuteChanged());
            }
        }
    }

    public Version NewVersion
    {
        get => _newVersion;
        private set => SetProperty(ref _newVersion, value);
    }

    internal override void OnActivated()
    {
        base.OnActivated();

        _updateService.StartCheckingForUpdate();
    }

    private void HandleUpdating(IAppUpdateState state)
    {
        if (state.Status == AppUpdateStatus.Updating)
        {
            _app.ExitAsync();
        }
    }

    private static UpdateStatus ToUpdateStatus(IAppUpdateState state)
    {
        return state.Status switch
        {
            AppUpdateStatus.None => UpdateStatus.UpToDate,
            AppUpdateStatus.Checking => UpdateStatus.Checking,
            AppUpdateStatus.CheckFailed => UpdateStatus.CheckFailed,
            AppUpdateStatus.Downloading => UpdateStatus.Downloading,
            AppUpdateStatus.DownloadFailed => UpdateStatus.DownloadFailed,
            AppUpdateStatus.Ready => UpdateStatus.Available,
            AppUpdateStatus.Updating => UpdateStatus.Available,
            _ => throw new NotSupportedException(),
        };
    }

    private static IEnumerable<ReleaseNoteViewModel> GetReleaseNotes(IAppUpdateState state)
    {
        return state.ReleaseHistory.Select(
            x =>
            {
                var sections = x.ReleaseNotes
                    .Select(
                        sectionGroup => new ReleaseNoteSectionViewModel
                        {
                            Type = sectionGroup.Type,
                            Notes = sectionGroup.Notes.ToList(),
                        }).ToList();

                return new ReleaseNoteViewModel
                {
                    Version = x.Version,
                    ReleaseDate = x.ReleaseDate,
                    IsNewVersion = x.IsNew,
                    Sections = sections,
                };
            });
    }

    private void OpenPrivacyPolicy()
    {
        _externalHyperlinks.PrivacyPolicy.Open();
    }

    private void OpenTermsAndConditions()
    {
        _externalHyperlinks.TermsAndConditions.Open();
    }

    private void Update()
    {
        if (!CanUpdate())
        {
            return;
        }

        _updateService.StartUpdating();
    }

    private bool CanUpdate()
    {
        return UpdateStatus == UpdateStatus.Available;
    }

    private void OnUpdateServiceStateChanged(object? sender, UpdateState state)
    {
        UpdateStatus = ToUpdateStatus(state);

        HandleReleaseDate(state);
        HandleNewVersion(state);
        HandleUpdating(state);
        HandleReleaseNotes(state);
    }

    private void HandleReleaseDate(IAppUpdateState state)
    {
        if (ReleaseDate != default)
        {
            return;
        }

        var currentRelease = state.ReleaseHistory.FirstOrDefault(r => r.Version.Equals(CurrentVersion));
        if (currentRelease != null)
        {
            ReleaseDate = currentRelease.ReleaseDate;
        }
    }

    private void HandleReleaseNotes(IAppUpdateState state)
    {
        _scheduler.Schedule(
            () =>
            {
                var releaseNotes = GetReleaseNotes(state);
                ReleaseNotes = releaseNotes.ToList();
            });
    }

    private void HandleNewVersion(IAppUpdateState state)
    {
        if (state.Status == AppUpdateStatus.Ready)
        {
            NewVersion = state.ReleaseHistory.First().Version;
        }
    }
}
