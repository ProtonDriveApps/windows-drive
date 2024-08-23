using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Services;

public interface IStoppableService
{
    public Task StopAsync(CancellationToken cancellationToken);
}
