namespace Proton.Drive.Sdk.Sync.Shared.Authentication;

public interface IStatefulSessionService
{
    Task StartSessionAsync();
    Task EndSessionAsync();
}
