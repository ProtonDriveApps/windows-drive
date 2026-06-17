using ProtonDrive.Client;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.Notifications;

internal sealed class NotificationClient : INotificationClient
{
    private readonly ICollectionRepository<Contracts.Notification> _repository;

    public NotificationClient(ICollectionRepository<Contracts.Notification> repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<Contracts.Notification>> GetNotificationsAsync(string userSubscriptionPlanCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var notifications = _repository
                .GetAll()
                .Where(n => n.UserSubscriptionPlanCodes.Contains(userSubscriptionPlanCode))
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyCollection<Contracts.Notification>>(notifications);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            throw new ApiException("Retrieving notifications failed", ex);
        }
    }
}
