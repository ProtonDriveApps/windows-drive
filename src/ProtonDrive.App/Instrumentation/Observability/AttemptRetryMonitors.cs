using System.Collections.ObjectModel;

namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class AttemptRetryMonitors
{
    public IReadOnlyDictionary<AttemptRetryShareType, AttemptRetryMonitor<long>> UploadAttemptRetryMonitor { get; } = CreateMonitor();
    public IReadOnlyDictionary<AttemptRetryShareType, AttemptRetryMonitor<long>> DownloadAttemptRetryMonitor { get; } = CreateMonitor();

    public void Clear()
    {
        foreach (var monitor in UploadAttemptRetryMonitor.Values)
        {
            monitor.Clear();
        }

        foreach (var monitor in DownloadAttemptRetryMonitor.Values)
        {
            monitor.Clear();
        }
    }

    private static ReadOnlyDictionary<AttemptRetryShareType, AttemptRetryMonitor<long>> CreateMonitor()
    {
        var attemptRetryMonitors = new Dictionary<AttemptRetryShareType, AttemptRetryMonitor<long>>
        {
            { AttemptRetryShareType.Main, new AttemptRetryMonitor<long>() },
            { AttemptRetryShareType.Device, new AttemptRetryMonitor<long>() },
            { AttemptRetryShareType.Standard, new AttemptRetryMonitor<long>() },
            { AttemptRetryShareType.Photo, new AttemptRetryMonitor<long>() },
        };

        return attemptRetryMonitors.AsReadOnly();
    }
}
