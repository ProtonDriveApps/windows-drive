namespace ProtonDrive.App.Windows.Views.Shared.Notification;

internal sealed class NotificationBadge
{
    public NotificationBadge(string symbol, string description, NotificationBadgeSeverity severity)
    {
        Symbol = symbol;
        Description = description;
        Severity = severity;
    }

    public string Symbol { get; }

    public string Description { get; }

    public NotificationBadgeSeverity Severity { get; }
}
