namespace Proton.Drive.Sdk.Sync.Client.Core.Events;

public interface ICoreEventProvider
{
    event EventHandler<CoreEvents> EventsReceived;
}
