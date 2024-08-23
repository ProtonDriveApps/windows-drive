using System;

namespace ProtonDrive.App.Notifications;

public class NotificationActivatedEventArgs : EventArgs
{
    public string Id { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}
