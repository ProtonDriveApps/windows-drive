namespace ProtonDrive.Client.Contracts;

public sealed record SubscriptionResponse : ApiResponse
{
    private UserSubscription? _subscription;

    public UserSubscription Subscription
    {
        get => _subscription ??= new UserSubscription();
        init => _subscription = value;
    }
}
