using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Teardown;

internal sealed class LocalSpecialFoldersDeletionStep : ILocalSpecialSubfoldersDeletionStep
{
    private readonly AppConfig _appConfig;
    private readonly ILogger<LocalSpecialFoldersDeletionStep> _logger;

    public LocalSpecialFoldersDeletionStep(
        AppConfig appConfig,
        ILogger<LocalSpecialFoldersDeletionStep> logger)
    {
        _appConfig = appConfig;
        _logger = logger;
    }

    public void DeleteSpecialSubfolders(string? rootPath)
    {
        if (string.IsNullOrEmpty(rootPath))
        {
            return;
        }

        TryDeleteSubfolder(
            rootPath,
            Path.Combine(_appConfig.FolderNames.TempFolderName, _appConfig.FolderNames.TrashFolderName),
            recursive: true);

        TryDeleteSubfolder(
            rootPath,
            _appConfig.FolderNames.TempFolderName,
            recursive: false);
    }

    private void TryDeleteSubfolder(string rootPath, string relativePath, bool recursive)
    {
        var path = Path.Combine(rootPath, relativePath);

        if (!Directory.Exists(path))
        {
            return;
        }

        if (recursive)
        {
            _logger.LogInformation("Deleting special folder \"{Name}\" with all content", relativePath);
        }
        else
        {
            _logger.LogInformation("Deleting special folder \"{Name}\"", relativePath);
        }

        try
        {
            Directory.Delete(path, recursive);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to delete special folder \"{Name}\" on the local replica root: {ExceptionType}: {ErrorCode}", relativePath, ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
        }
    }
}
