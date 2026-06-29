namespace Proton.Drive.App;

public interface IApp
{
    Task<IntPtr> ActivateAsync();

    Task RestartAsync();

    Task ExitAsync();
}
