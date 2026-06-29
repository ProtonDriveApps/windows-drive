namespace Proton.Drive.App.InterProcessCommunication;

public interface IIpcResponder
{
    Task Respond<T>(T value, CancellationToken cancellationToken);
}
