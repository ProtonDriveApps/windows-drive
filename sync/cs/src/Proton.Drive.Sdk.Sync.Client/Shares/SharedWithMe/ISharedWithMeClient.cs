namespace Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe;

public interface ISharedWithMeClient
{
    IAsyncEnumerable<SharedWithMeItem?> GetSharedWithMeItemsAsync(CancellationToken cancellationToken);

    Task<SharedWithMeItem?> GetSharedWithMeItemAsync(string shareId, CancellationToken cancellationToken);

    Task RemoveMemberAsync(string shareId, string memberId, CancellationToken cancellationToken);
}
