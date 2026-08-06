namespace Proton.Drive.App.EarlyAccess;

public interface IEarlyAccessStateAware
{
    void OnEarlyAccessStateChanged(EarlyAccessStatus status);
}
