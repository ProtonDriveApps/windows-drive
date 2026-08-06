using Proton.Drive.Sdk.Sync.Shared.Diagnostics.TemporaryFiles;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry;

public sealed class ExcelTemporaryFileDetectionCounter : IExcelTemporaryFileDetectionCounter, IExcelTemporaryFileDetectionCountProvider
{
    private int _numberOfDetectedExcelTemporaryFileCandidates;

    public void Increment()
    {
        Interlocked.Increment(ref _numberOfDetectedExcelTemporaryFileCandidates);
    }

    public int GetCount()
    {
        return _numberOfDetectedExcelTemporaryFileCandidates;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _numberOfDetectedExcelTemporaryFileCandidates, 0);
    }
}
