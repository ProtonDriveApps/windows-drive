using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.App.Windows.Diagnostics.Telemetry.FirstLaunch;

internal sealed class FirstLaunchReportProvider(ILogger<FirstLaunchReportProvider> logger) : IOneTimeTelemetryReportProvider
{
    private const string ProtonDriveRegistryKeyName = @"Software\Proton\Drive";
    private const string SourceRegistryValueName = "InstallationInitiator";
    private const string ReportRegistryValueName = "FirstLaunch";

    private bool _sendingOneTimeReport;

    public IEnumerable<TelemetryEvent> GetOneTimeReport()
    {
        if (!TryGetInstallationInitiator(out var initiator))
        {
            yield break;
        }

        _sendingOneTimeReport = true;
        yield return FirstLaunchReportFactory.CreateEvent(initiator);
    }

    public void OnOneTimeReportSent()
    {
        if (!_sendingOneTimeReport)
        {
            return;
        }

        _sendingOneTimeReport = false;
        MarkReportSent();
    }

    private bool TryGetInstallationInitiator([MaybeNullWhen(false)] out string initiator)
    {
        initiator = null;

        try
        {
            using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(ProtonDriveRegistryKeyName, writable: false);

            if (registryKey is null)
            {
                return false;
            }

            if (!FirstLaunchReportMustBeSent(registryKey, ReportRegistryValueName))
            {
                return false;
            }

            var initiatorValue = registryKey.GetValue(SourceRegistryValueName) as string;
            initiator = !string.IsNullOrWhiteSpace(initiatorValue) ? initiatorValue : "own";

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to produce installation source report: {Message}", ex.Message);
        }

        return false;

        static bool FirstLaunchReportMustBeSent(RegistryKey registryKey, string value)
        {
            return registryKey.GetValue(value) is 0;
        }
    }

    private void MarkReportSent()
    {
        try
        {
            using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(ProtonDriveRegistryKeyName, writable: true);

            if (registryKey is null)
            {
                return;
            }

            // Mark the installation source report as sent by setting the registry value to 1.
            // This ensures that the report is not sent multiple times during the lifetime of the application instance.
            registryKey.SetValue(ReportRegistryValueName, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to mark installation source report sent: {Message}", ex.Message);
        }
    }
}
