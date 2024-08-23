using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client;
using ProtonDrive.Client.BugReport;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Offline;

namespace ProtonDrive.App.Reporting;

internal class BugReportService : IBugReportService
{
    private const int NumberOfLogFilesToSend = 3;
    private const int BufferSize = 4_096;

    private readonly AppConfig _appConfig;
    private readonly IBugReportClient _bugReportClient;
    private readonly IOfflineService _offlineService;
    private readonly ILogger<BugReportService> _logger;

    public BugReportService(
        AppConfig appConfig,
        IBugReportClient bugReportClient,
        IOfflineService offlineService,
        ILogger<BugReportService> logger)
    {
        _appConfig = appConfig;
        _bugReportClient = bugReportClient;
        _offlineService = offlineService;
        _logger = logger;
    }

    public async Task<Result> SendAsync(BugReportBody body, bool includeLogs, CancellationToken cancellationToken)
    {
        _offlineService.ForceOnline();

        Stream? attachment = null;

        try
        {
            if (includeLogs)
            {
                attachment = await GetLogsFileAsync(cancellationToken).ConfigureAwait(false);
                attachment.Seek(0, SeekOrigin.Begin);
            }

            await _bugReportClient.SendAsync(body, attachment, cancellationToken).ConfigureAwait(false);

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
            if (attachment is not null)
            {
                await attachment.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<FileStream> GetLogsFileAsync(CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var fileStream = File.Create(tempFile, BufferSize, FileOptions.DeleteOnClose);

        var logFolderPath = Path.Combine(_appConfig.AppDataPath, "Logs");

        var logFiles = Directory.EnumerateFiles(logFolderPath, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Take(NumberOfLogFilesToSend);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, leaveOpen: true);

        foreach (var file in logFiles)
        {
            ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(file));

            await using var stream = entry.Open();

            await using var logFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            await logFile.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        return fileStream;
    }
}
