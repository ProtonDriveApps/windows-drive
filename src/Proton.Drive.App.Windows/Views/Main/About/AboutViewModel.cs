using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Update;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Toolkit.Threading;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Update;
using ICommand = System.Windows.Input.ICommand;

namespace Proton.Drive.App.Windows.Views.Main.About;

internal sealed class AboutViewModel : PageViewModel
{
    private const string DefaultCopyrightMessage = "© Proton AG";

    private readonly IApp _app;
    private readonly IUpdateService _updateService;
    private readonly DispatcherScheduler _scheduler;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly RelayCommand _updateCommand;

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

        CurrentVersion = config.AppVersion;
        ReleaseNotes = [];
        NewVersion = new Version();

        OpenPrivacyPolicyCommand = new RelayCommand(OpenPrivacyPolicy);
        OpenTermsAndConditionsCommand = new RelayCommand(OpenTermsAndConditions);

        updateService.StateChanged += OnUpdateServiceStateChanged;

        _updateCommand = new RelayCommand(Update, CanUpdate);
    }

    public ICommand UpdateCommand => _updateCommand;

    public ICommand OpenPrivacyPolicyCommand { get; }

    public ICommand OpenTermsAndConditionsCommand { get; }

    public Version CurrentVersion { get; }

    public IReadOnlyList<ReleaseNoteViewModel> ReleaseNotes
    {
        get;
        private set => SetProperty(ref field, value);
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
        get;
        private set => SetProperty(ref field, value);
    }

    public AppUpdateStatus AppUpdateStatus
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                _scheduler.Schedule(() => _updateCommand.NotifyCanExecuteChanged());
            }
        }
    }

    public bool UpdateRequired
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public Version NewVersion
    {
        get;
        private set => SetProperty(ref field, value);
    }

    internal override void OnActivated()
    {
        base.OnActivated();

        _updateService.StartCheckingForUpdate();
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

    private AppUpdateStatus ToAppUpdateStatus(UpdateState state)
    {
        UpdateRequired = state.UpdateRequired;

        return state.Status switch
        {
            AppUpdateStatus.Updating => AppUpdateStatus.Ready,
            _ => state.Status,
        };
    }

    private void HandleUpdating(UpdateState state)
    {
        if (state.Status == AppUpdateStatus.Updating)
        {
            _app.ExitAsync();
        }
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
        return AppUpdateStatus is AppUpdateStatus.Ready;
    }

    private void OnUpdateServiceStateChanged(object? sender, UpdateState state)
    {
        _scheduler.Schedule(() =>
        {
            AppUpdateStatus = ToAppUpdateStatus(state);

            HandleReleaseDate(state);
            HandleNewVersion(state);
            HandleUpdating(state);
            HandleReleaseNotes(state);
        });
    }

    private void HandleReleaseDate(UpdateState state)
    {
        if (ReleaseDate != null)
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
        _scheduler.Schedule(() =>
        {
            var releaseNotes = GetReleaseNotes(state);
            ReleaseNotes = releaseNotes.ToList();
        });
    }

    private void HandleNewVersion(UpdateState state)
    {
        if (state.Status == AppUpdateStatus.Ready)
        {
            NewVersion = state.ReleaseHistory[0].Version;
        }
    }
}
