namespace ProtonDrive.App.Windows.SystemIntegration;

internal interface IOperatingSystemIntegrationService
{
    void SetRunApplicationOnStartup(bool value);
    bool GetRunApplicationOnStartup();
}
