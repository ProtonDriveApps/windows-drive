namespace ProtonDrive.Client.Events;

internal sealed class DriveEventResumeToken
{
    public static DriveEventResumeToken Start { get; } = new();

    public bool HasMoreData { get; internal init; }
    public bool IsRefreshRequired { get; internal init; }

    internal string? AnchorId { get; init; }

    public override string? ToString() => AnchorId;
}
