namespace ProtonDrive.Sync.Shared;

public interface IIdentitySource<TId>
{
    TId NextValue();
    void InitializeFrom(TId? value);
}
