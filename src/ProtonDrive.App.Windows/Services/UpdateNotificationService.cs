using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Notifications;
using ProtonDrive.App.Services;
using ProtonDrive.App.Update;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Views.Main;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Update;

namespace ProtonDrive.App.Windows.Services;

internal sealed class UpdateNotificationService : IStartableService
{
    public const string NotificationGroupId = "Update";
    public const string NotificationId = "Update";
    public const string UpdateActionName = "Update";
    public const string DownloadUpdateActionName = "DownloadUpdate";
    public const string RemindLaterActionName = "RemindLater";

    private readonly UpdateConfig _updateConfig;
    private readonly IExternalHyperlinks _hyperlinks;
    private readonly IUpdateService _updateService;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPages _appPages;

    private bool _updateReady;
    private DateTime _lastNotifiedAt;
    private UpdateNotificationType _lastNotificationType;

    public UpdateNotificationService(
        UpdateConfig updateConfig,
        IExternalHyperlinks hyperlinks,
        IUpdateService updateService,
        INotificationService notificationService,
        IApplicationPages appPages)
    {
        _updateConfig = updateConfig;
        _hyperlinks = hyperlinks;
        _updateService = updateService;
        _notificationService = notificationService;
        _appPages = appPages;

        _updateService.StateChanged += UpdateServiceOnStateChanged;
        _notificationService.NotificationActivated += NotificationServiceOnNotificationActivated;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void UpdateServiceOnStateChanged(object? sender, UpdateState state)
    {
        _updateReady = state.IsReady;

        if (state.UpdateRequired)
        {
            HandleUpdateRequiredNotification(state);
        }
        else
        {
            HandleUpdateNotification(state);
        }
    }

    private void HandleUpdateRequiredNotification(UpdateState state)
    {
        if (state.IsReady)
        {
            ShowNotificationIfNotYetShown(UpdateNotificationType.UpdateRequiredAndReady);
        }

        // Suppress UpdateRequired notification while checking for update
        else if (state.Status is not AppUpdateStatus.Checking)
        {
            ShowNotificationIfNotYetShown(UpdateNotificationType.UpdateRequired);
        }
    }

    private void HandleUpdateNotification(UpdateState state)
    {
        var status = state.Status;

        if (status == AppUpdateStatus.Ready && IsTimeToNotify())
        {
            ShowNotification(UpdateNotificationType.UpdateReady);
        }
        else if (status == AppUpdateStatus.None)
        {
            RemoveNotification();
        }
    }

    private bool IsTimeToNotify()
    {
        return _lastNotifiedAt + _updateConfig.NotificationInterval < DateTime.UtcNow;
    }

    private void ShowNotificationIfNotYetShown(UpdateNotificationType type)
    {
        if (_lastNotificationType != type)
        {
            ShowNotification(type);
        }
    }

    private void ShowNotification(UpdateNotificationType type)
    {
        var notification = type switch
        {
            UpdateNotificationType.UpdateReady => UpdateNotification(),
            UpdateNotificationType.UpdateRequired => UpdateRequiredNotification(),
            UpdateNotificationType.UpdateRequiredAndReady => UpdateRequiredAndReadyNotification(),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        _notificationService.ShowNotification(notification);

        _lastNotifiedAt = DateTime.UtcNow;
        _lastNotificationType = type;
    }

    private Notification UpdateNotification()
    {
        return new Notification()
            .SetGroup(NotificationGroupId)
            .SetId(NotificationId)
            .SetHeaderText("A new version of Proton Drive is available!")
            .SetText("Click here for more details.")
            .AddButton("Update", UpdateActionName)
            .AddButton("Remind me later", RemindLaterActionName);
    }

    private Notification UpdateRequiredNotification()
    {
        return BaseUpdateRequiredNotification()
            .AddButton("Download and install update", DownloadUpdateActionName);
    }

    private Notification UpdateRequiredAndReadyNotification()
    {
        return BaseUpdateRequiredNotification()
            .AddButton("Update", UpdateActionName);
    }

    private Notification BaseUpdateRequiredNotification()
    {
        return new Notification()
            .SetGroup(NotificationGroupId)
            .SetId(NotificationId)
            .SetHeaderText("Update required")
            .SetText("To keep using Proton Drive, you’ll need to update to the latest version.");
    }

    private void NotificationServiceOnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {
        if (e.GroupId != NotificationGroupId)
        {
            return;
        }

        switch (e.Action)
        {
            // The user pressed "Update" in the notification
            case UpdateActionName:
                if (_updateReady)
                {
                    Update();
                }
                else
                {
                    ShowDetails();
                }

                break;

            // The user pressed "Remind later" in the notification
            case RemindLaterActionName:
                break;

            // The user pressed "Download an update" in the notification
            case DownloadUpdateActionName:
                DownloadUpdate();
                break;

            // The user pressed the update notification but not the specific button
            default:
                ShowDetails();
                break;
        }
    }

    private void RemoveNotification()
    {
        _notificationService.RemoveNotificationGroup(NotificationGroupId);
    }

    private void ShowDetails()
    {
        _appPages.Show(ApplicationPage.About);
    }

    private void Update()
    {
        _updateService.StartUpdating();
    }

    private void DownloadUpdate()
    {
        _hyperlinks.AppDownloadPage.Open();
    }
}
