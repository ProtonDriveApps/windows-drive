using System;

namespace ProtonDrive.Client.Core.Events.Contracts;

[Flags]
internal enum CoreEventsRefreshMask
{
    None = 0,
    Mail = 1,
    Contacts = 2,
    Everything = 255,
}
