using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Services;

public interface IStartableService
{
    public Task StartAsync(CancellationToken cancellationToken);
}
