namespace Proton.Drive.Sdk.Sync.Client.Authentication.Sessions;

public interface ISessionClient
{
    Task<string> ForkSessionAsync(SessionForkingParameters parameters, CancellationToken cancellationToken);
}
