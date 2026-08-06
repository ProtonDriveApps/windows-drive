using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.BugReport;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Offline;

namespace Proton.Drive.App.Reporting;

internal class BugReportService : IBugReportService
{
    private const int MaxNumberOfAppLogFilesToSend = 3;
    private const int MaxNumberOfInstallationLogFilesToSend = 20;
    private const int BufferSize = 4_096;

    private static readonly string TempFolderPath = Path.GetTempPath();

    private readonly IBugReportClient _bugReportClient;
    private readonly IOfflineService _offlineService;
    private readonly ILogger<BugReportService> _logger;
    private readonly string _logsFolderPath;
    private readonly string _installationLogsFolderPath;

    public BugReportService(
        AppConfig appConfig,
        IBugReportClient bugReportClient,
        IOfflineService offlineService,
        ILogger<BugReportService> logger)
    {
        _bugReportClient = bugReportClient;
        _offlineService = offlineService;
        _logger = logger;

        _logsFolderPath = Path.Combine(appConfig.AppDataPath, "Logs");
        _installationLogsFolderPath = Path.Combine(_logsFolderPath, "Installation");
    }

    public async Task<Result> SendAsync(BugReportBody body, bool includeLogs, CancellationToken cancellationToken)
    {
        _offlineService.ForceOnline();

        IReadOnlyCollection<BugReportAttachment> attachments = Array.Empty<BugReportAttachment>();

        try
        {
            if (includeLogs)
            {
                attachments = await GetAttachmentsAsync(cancellationToken).ConfigureAwait(false);
            }

            await _bugReportClient.SendAsync(body, attachments, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogError("Failed attach app logs: {ErrorMessage}", ex.CombinedMessage());

            return Result.Failure(ex.Message);
        }
        catch (ApiException ex)
        {
            _logger.LogError("Failed to send bug report: {ErrorMessage}", ex.CombinedMessage());

            return Result.Failure(ex.Message);
        }
        finally
        {
            foreach (var attachment in attachments)
            {
                await attachment.Stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<FileStream> GetZippedFileStreamAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(TempFolderPath, Path.GetRandomFileName());

        var fileStream = File.Create(tempFilePath, BufferSize, FileOptions.DeleteOnClose);

        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var filePath in filePaths)
            {
                ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(filePath), CompressionLevel.SmallestSize);

                var stream = entry.Open();
                await using (stream.ConfigureAwait(false))
                {
                    var logFile = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await using (logFile.ConfigureAwait(false))
                    {
                        await logFile.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        fileStream.Seek(0, SeekOrigin.Begin);

        return fileStream;
    }

    private async Task<IReadOnlyCollection<BugReportAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken)
    {
        var attachments = new List<BugReportAttachment>(2);

        var appLogAttachmentStream = await GetAppLogFileStreamAsync(cancellationToken).ConfigureAwait(false);

        attachments.Add(new BugReportAttachment("App-Logs", "Drive-AppLogs.zip", appLogAttachmentStream));

        var installationLogAttachmentStream = await GetInstallationLogFileStreamAsync(cancellationToken).ConfigureAwait(false);

        if (installationLogAttachmentStream is not null)
        {
            attachments.Add(new BugReportAttachment("Installation-Logs", "Drive-InstallationLogs.zip", installationLogAttachmentStream));
        }

        return attachments;
    }

    private async Task<FileStream> GetAppLogFileStreamAsync(CancellationToken cancellationToken)
    {
        var logFiles = Directory.EnumerateFiles(_logsFolderPath, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Take(MaxNumberOfAppLogFilesToSend);

        return await GetZippedFileStreamAsync(logFiles, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FileStream?> GetInstallationLogFileStreamAsync(CancellationToken cancellationToken)
    {
        var logFiles = Directory.EnumerateFiles(_installationLogsFolderPath)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Take(MaxNumberOfInstallationLogFilesToSend)
            .ToList();

        if (logFiles.Count == 0)
        {
            return null;
        }

        return await GetZippedFileStreamAsync(logFiles, cancellationToken).ConfigureAwait(false);
    }
}
