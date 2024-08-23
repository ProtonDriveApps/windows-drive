using System.Diagnostics;

namespace ProtonDrive.Shared.Diagnostics;

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo StandardInfo(this ProcessStartInfo info, ProcessWindowStyle windowStyle)
    {
        info.CreateNoWindow = false;
        info.UseShellExecute = true;
        info.WindowStyle = windowStyle;

        return info;
    }

    public static ProcessStartInfo ElevatedInfo(this ProcessStartInfo info)
    {
        info.CreateNoWindow = true;
        info.UseShellExecute = true;
        info.RedirectStandardOutput = false;
        info.Verb = "runas";

        return info;
    }
}
