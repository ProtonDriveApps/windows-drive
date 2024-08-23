using System;

namespace ProtonDrive.Client.Core.Events;

public interface ICoreEventProvider
{
    event EventHandler<CoreEvents> EventsReceived;
}
