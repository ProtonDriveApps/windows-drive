using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Core.Events;

public sealed class CoreEvents
{
    internal CoreEvents(CoreEventResumeToken resumeToken)
    {
        ResumeToken = resumeToken;
    }

    public CoreEventResumeToken ResumeToken { get; internal init; }

    public bool HasAddressChanged { get; internal init; }

    public User? User { get; internal init; }

    public Organization? Organization { get; internal init; }

    public UserSubscription? Subscription { get; internal init; }

    public long? UsedSpace { get; internal init; }
}
