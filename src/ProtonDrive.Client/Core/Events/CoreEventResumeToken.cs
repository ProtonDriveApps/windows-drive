namespace ProtonDrive.Client.Core.Events;

public sealed class CoreEventResumeToken
{
    public static CoreEventResumeToken Start { get; } = new();

    public bool HasMoreData { get; internal init; }
    public bool IsRefreshRequired { get; internal init; }

    internal string? AnchorId { get; init; }

    public override string? ToString() => AnchorId;
}
