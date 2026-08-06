namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal static class TransferPerformanceMonitorExtensions
{
    public static double? GetTransferSpeedInKibibytesPerSecond(this TransferPerformanceMonitor monitor) => monitor.GetTransferSpeed() / 1024;
}
