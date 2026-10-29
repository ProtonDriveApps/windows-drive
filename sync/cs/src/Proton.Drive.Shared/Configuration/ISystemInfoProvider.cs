namespace Proton.Drive.Shared.Configuration;

public interface ISystemInfoProvider
{
    /// <summary>
    /// The operating system and processor architecture description included in the User-Agent HTTP header.
    /// </summary>
    string SystemInfo { get; }
}
