namespace ProtonDrive.App.Notifications;

public interface INotificationClient
{
    Task<IReadOnlyCollection<Contracts.Notification>> GetNotificationsAsync(string userSubscriptionPlanCode, CancellationToken cancellationToken);
}
