using System.Threading.Tasks;

namespace ProtonDrive.App.Authentication;

public interface IStatefulSessionService
{
    Task StartSessionAsync();
    Task EndSessionAsync();
}
