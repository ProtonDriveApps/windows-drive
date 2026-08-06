namespace Proton.Drive.App.Settings;

public sealed class NotificationSettings
{
    private IReadOnlyCollection<ShownNotification>? _notifications;

    public IReadOnlyCollection<ShownNotification> ShownNotifications
    {
        get => _notifications ??= [];
        init => _notifications = value;
    }

    public sealed class ShownNotification
    {
        public required string Id { get; init; }
        public DateTime ShowingTimeUtc { get; init; }
    }
}
