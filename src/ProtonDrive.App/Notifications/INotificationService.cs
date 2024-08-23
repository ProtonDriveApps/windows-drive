using System;

namespace ProtonDrive.App.Notifications;

public interface INotificationService
{
    event EventHandler<NotificationActivatedEventArgs> NotificationActivated;

    void ShowNotification(Notification notification);
    void ShowReadyToBePastedNotification(string headerText, string text);
    void RemoveNotificationGroup(string group);
    void RemoveNotification(string group, string id);
}
