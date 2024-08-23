using System;

namespace ProtonDrive.Shared.Diagnostics;

public interface IOsProcess : IDisposable
{
    void Start();
}
