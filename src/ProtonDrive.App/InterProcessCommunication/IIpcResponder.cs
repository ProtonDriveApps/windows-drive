using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.InterProcessCommunication;

public interface IIpcResponder
{
    Task Respond<T>(T value, CancellationToken cancellationToken);
}
