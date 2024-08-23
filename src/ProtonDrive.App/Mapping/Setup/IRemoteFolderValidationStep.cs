using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal interface IRemoteFolderValidationStep
{
    Task<MappingErrorCode?> ValidateAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken);
}
