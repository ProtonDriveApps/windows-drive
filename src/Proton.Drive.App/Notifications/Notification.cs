namespace Proton.Drive.App.Notifications;

public sealed class Notification
{
    public string? Id { get; set; }
    public string? GroupId { get; set; }
    public string? HeaderText { get; set; }
    public string? Text { get; set; }
    public string? LogoImageUrl { get; set; }
    public DateTimeOffset? ExpirationTime { get; set; }
    public bool SuppressPopup { get; set; }
    public IList<NotificationButton> Buttons { get; } = new List<NotificationButton>();
}
