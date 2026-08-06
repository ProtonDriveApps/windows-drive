namespace Proton.Drive.App.Notifications;

public static class NotificationExtensions
{
    public static Notification SetId(this Notification notification, string id)
    {
        notification.Id = id;

        return notification;
    }

    public static Notification SetGroup(this Notification notification, string groupId)
    {
        notification.GroupId = groupId;

        return notification;
    }

    public static Notification SetHeaderText(this Notification notification, string text)
    {
        notification.HeaderText = text;

        return notification;
    }

    public static Notification SetText(this Notification notification, string text)
    {
        notification.Text = text;

        return notification;
    }

    public static Notification SetLogoImage(this Notification notification, string imageUrl)
    {
        notification.LogoImageUrl = imageUrl;

        return notification;
    }

    public static Notification SetExpirationTime(this Notification notification, DateTimeOffset? expirationTime)
    {
        notification.ExpirationTime = expirationTime;

        return notification;
    }

    public static Notification SuppressPopup(this Notification notification)
    {
        notification.SuppressPopup = true;

        return notification;
    }

    public static Notification AddButton(this Notification notification, string content, string action)
    {
        notification.Buttons.Add(new NotificationButton { Content = content, Action = action });

        return notification;
    }
}
