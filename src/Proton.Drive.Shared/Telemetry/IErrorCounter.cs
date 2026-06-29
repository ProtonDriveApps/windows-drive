namespace Proton.Drive.Shared.Telemetry;

public interface IErrorCounter
{
    void Add(ErrorScope scope, Exception exception);
}
