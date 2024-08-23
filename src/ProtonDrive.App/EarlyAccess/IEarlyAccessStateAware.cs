namespace ProtonDrive.App.EarlyAccess;

public interface IEarlyAccessStateAware
{
    void OnEarlyAccessStateChanged(EarlyAccessStatus status);
}
