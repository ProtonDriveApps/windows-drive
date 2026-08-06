namespace Proton.Drive.App.Notifications.Offers;

public sealed record NotificationMessage
{
    public required string HeaderText { get; init; }
    public required string ContentText { get; init; }
    public string? ButtonText { get; init; }
    public string? LogoImageFilePath { get; init; }
}
