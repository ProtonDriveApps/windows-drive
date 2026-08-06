namespace Proton.Drive.Sdk.Sync.Shared.Diagnostics.TemporaryFiles;

public interface IExcelTemporaryFileDetectionCountProvider
{
    int GetCount();
    void Reset();
}
