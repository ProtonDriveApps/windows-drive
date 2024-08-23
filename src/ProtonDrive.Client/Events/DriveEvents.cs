using System;
using System.Collections.Generic;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Events;

internal sealed class DriveEvents
{
    internal DriveEvents(DriveEventResumeToken resumeToken, IReadOnlyList<EventListItem>? events = null)
    {
        Events = events ?? Array.Empty<EventListItem>();
        ResumeToken = resumeToken;
    }

    public DriveEventResumeToken ResumeToken { get; }
    public IReadOnlyList<EventListItem> Events { get; }
}
