using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;
using Proton.Drive.App.Notifications;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Threading;
using Windows.UI.Notifications;
using Notification = Proton.Drive.App.Notifications.Notification;

namespace Proton.Drive.App.Windows.SystemIntegration;

internal sealed class SystemToastNotificationService : INotificationService
{
    private const string IdKey = "Id";
    private const string GroupKey = "Group";
    private const string ActionKey = "Action";

    private readonly AppConfig _appConfig;

    private readonly IScheduler _scheduler = new SerialScheduler();

    public SystemToastNotificationService(AppConfig appConfig)
    {
        _appConfig = appConfig;

        ToastNotificationManagerCompat.OnActivated += OnToastNotificationManagerCompatActivated;
    }

    public event EventHandler<NotificationActivatedEventArgs>? NotificationActivated;

    /// <summary>
    /// Clean up all Windows Toast notifications and notification-related resources.
    /// Call this when the app is being uninstalled.
    /// </summary>
    public static void Uninstall()
    {
        Safe(ToastNotificationManagerCompat.Uninstall);
    }

    public void ShowNotification(Notification notification)
    {
        Schedule(() => Safe(() => UnsafeShowNotification(notification)));
    }

    public void RemoveNotificationGroup(string groupId)
    {
        Schedule(() => Safe(() => ToastNotificationManager.History.RemoveGroup(groupId, _appConfig.ApplicationId)));
    }

    public void RemoveNotification(string groupId, string id)
    {
        Schedule(() => Safe(() => ToastNotificationManager.History.Remove(id, groupId, _appConfig.ApplicationId)));
    }

    private static void Safe(Action action)
    {
        try
        {
            action.Invoke();
        }
        catch (Exception ex) when (ex is COMException or UriFormatException)
        {
            // Ignore
        }
    }

    private void OnToastNotificationManagerCompatActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        // Obtain arguments from the notification as a list of key-value pairs
        ToastArguments args = ToastArguments.Parse(e.Argument);

        OnNotificationActivated(new NotificationActivatedEventArgs
        {
            Id = args.TryGetValue(IdKey, out var id) ? id : string.Empty,
            GroupId = args.TryGetValue(GroupKey, out var groupId) ? groupId : string.Empty,
            Action = args.TryGetValue(ActionKey, out var action) ? action : string.Empty,
        });
    }

    private void OnNotificationActivated(NotificationActivatedEventArgs eventArgs)
    {
        NotificationActivated?.Invoke(this, eventArgs);
    }

    private void UnsafeShowNotification(Notification notification)
    {
        var builder = new ToastContentBuilder();

        if (!string.IsNullOrEmpty(notification.Id))
        {
            builder.AddArgument(IdKey, notification.Id);
        }

        if (!string.IsNullOrEmpty(notification.GroupId))
        {
            builder.AddArgument(GroupKey, notification.GroupId);
        }

        builder.AddText(notification.HeaderText ?? string.Empty, hintWrap: true);

        if (!string.IsNullOrEmpty(notification.Text))
        {
            builder.AddText(notification.Text);
        }

        var logoUri = !string.IsNullOrEmpty(notification.LogoImageUrl)
            ? new Uri(notification.LogoImageUrl)
            : new Uri(Path.Combine(_appConfig.AppFolderPath, "Logo.png"));

        builder.AddAppLogoOverride(logoUri);

        foreach (var button in notification.Buttons)
        {
            var toastButton = new ToastButton().SetContent(button.Content);

            if (!string.IsNullOrEmpty(button.Action))
            {
                toastButton.AddArgument(ActionKey, button.Action);
            }

            builder.AddButton(toastButton);
        }

        builder.Show(
            toast =>
            {
                if (!string.IsNullOrEmpty(notification.Id))
                {
                    toast.Tag = notification.Id;
                }

                if (!string.IsNullOrEmpty(notification.GroupId))
                {
                    toast.Group = notification.GroupId;
                }

                toast.ExpirationTime = notification.ExpirationTime;
                toast.SuppressPopup = notification.SuppressPopup;
            });
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
