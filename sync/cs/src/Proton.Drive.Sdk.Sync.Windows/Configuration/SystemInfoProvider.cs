using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Windows.Configuration;

public sealed class SystemInfoProvider : ISystemInfoProvider
{
    private readonly Lazy<string> _systemInfo = new(GetSystemInfo);

    public string SystemInfo => _systemInfo.Value;

    private static string GetSystemInfo()
    {
        var parts = new List<string>
        {
            $"Windows NT {Environment.OSVersion.Version.ToNormalized()}",
        };

        if (Environment.Is64BitOperatingSystem)
        {
            // System has a 64-bit processor
            parts.Add("Win64");
            parts.Add("x64");

            if (!Environment.Is64BitProcess)
            {
                // A 32-bit version of the app is running on the 64-bit processor
                parts.Add("WOW64");
            }
        }

        return string.Join("; ", parts);
    }
}
