namespace Proton.Drive.App.Notifications.Contracts;

public sealed class Offer
{
    public required string Title { get; init; }
    public required string ImageUrl { get; init; }
    public required string AccountAppUrl { get; init; }
}
