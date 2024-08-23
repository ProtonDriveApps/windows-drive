using System;
using System.Threading.Tasks;

namespace ProtonDrive.App;

public interface IApp
{
    Task<IntPtr> ActivateAsync();

    Task ExitAsync();
}
