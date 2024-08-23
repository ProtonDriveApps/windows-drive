using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;
using ProtonDrive.App.Notifications;
using ProtonDrive.Shared.Configuration;
using Windows.UI.Notifications;
using Notification = ProtonDrive.App.Notifications.Notification;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class SystemToastNotificationService : INotificationService
{
    private const string IdKey = "Id";
    private const string GroupKey = "Group";
    private const string ActionKey = "Action";
    private const string ReadyToBePastedNotificationId = "ReadyToBePastedNotificationId";

    private readonly AppConfig _appConfig;

    public SystemToastNotificationService(AppConfig appConfig)
    {
        _appConfig = appConfig;

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompatOnActivated;
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
        Safe(() => UnsafeShowNotification(notification));
    }

    public void ShowReadyToBePastedNotification(string headerText, string text)
    {
        var notification = new Notification
        {
            HeaderText = headerText,
            GroupId = ReadyToBePastedNotificationId,
            Id = ReadyToBePastedNotificationId,
            Text = text,
        };

        ShowNotification(notification);
    }

    public void RemoveNotificationGroup(string groupId)
    {
        Safe(() => ToastNotificationManager.History.RemoveGroup(groupId, _appConfig.ApplicationId));
    }

    public void RemoveNotification(string groupId, string id)
    {
        Safe(() => ToastNotificationManager.History.Remove(id, groupId, _appConfig.ApplicationId));
    }

    private static void Safe(Action action)
    {
        try
        {
            action.Invoke();
        }
        catch (COMException)
        {
            // Ignore
        }
    }

    private void ToastNotificationManagerCompatOnActivated(ToastNotificationActivatedEventArgsCompat e)
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

        builder.AddText(notification.HeaderText, hintWrap: true);

        if (!string.IsNullOrEmpty(notification.Text))
        {
            builder.AddText(notification.Text);
        }

        builder.AddAppLogoOverride(new Uri(Path.Combine(_appConfig.AppFolderPath, "Logo.png")));

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
            });
    }
}
