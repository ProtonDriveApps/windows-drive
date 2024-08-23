using System.Collections.Generic;

namespace ProtonDrive.App.Notifications;

public sealed class Notification
{
    public string Id { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string HeaderText { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public IList<NotificationButton> Buttons { get; } = new List<NotificationButton>();
}
