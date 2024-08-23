using System;

namespace ProtonDrive.Shared.Telemetry;

public interface IErrorCounter
{
    void Add(ErrorScope scope, Exception exception);

    void Reset();
}
