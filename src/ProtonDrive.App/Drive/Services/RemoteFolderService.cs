using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Drive.Services;

internal class RemoteFolderService : IRemoteFolderService
{
    private readonly IFolderApiClient _folderApiClient;
    private readonly ILinkApiClient _linkApiClient;

    public RemoteFolderService(IFolderApiClient folderApiClient, ILinkApiClient linkApiClient)
    {
        _folderApiClient = folderApiClient;
        _linkApiClient = linkApiClient;
    }

    public async Task<bool> FolderExistsAsync(string shareId, string linkId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _linkApiClient.GetLinkAsync(shareId, linkId, cancellationToken)
                .ThrowOnFailure()
                .ConfigureAwait(false);

            return result.Succeeded && result.Link is { Type: LinkType.Folder, State: LinkState.Active };
        }
        catch (ApiException ex) when (ex.ResponseCode == ResponseCode.DoesNotExist)
        {
            return false;
        }
    }

    public async Task<bool> NonEmptyFolderExistsAsync(string shareId, string linkId, CancellationToken cancellationToken)
    {
        var childListParameters = new FolderChildListParameters { PageIndex = 0, PageSize = 1, ShowAll = false };

        var response = await _folderApiClient.GetFolderChildrenAsync(shareId, linkId, childListParameters, cancellationToken)
            .ThrowOnFailure()
            .ConfigureAwait(false);

        return response.Links.Count == 0;
    }
}
