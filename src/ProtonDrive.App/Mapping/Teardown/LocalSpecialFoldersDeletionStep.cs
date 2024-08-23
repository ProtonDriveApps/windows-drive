using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Mapping.Teardown;

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

        _logger.LogInformation("Deleting special folders on the local replica root");

        TryDeleteSubfolder(
            rootPath,
            Path.Combine(_appConfig.FolderNames.TempFolderName, _appConfig.FolderNames.TrashFolderName),
            recursive: true,
            () => _logger.LogInformation("Deleting trash folder with all content"));

        TryDeleteSubfolder(
            rootPath,
            _appConfig.FolderNames.TempFolderName,
            recursive: false,
            () => _logger.LogInformation("Deleting temporary folder"));
    }

    private void TryDeleteSubfolder(string rootPath, string relativePath, bool recursive, Action loggingAction)
    {
        var path = Path.Combine(rootPath, relativePath);

        if (!Directory.Exists(path))
        {
            return;
        }

        loggingAction.Invoke();

        try
        {
            Directory.Delete(path, recursive);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to delete special folder \"{Path}\" on the local replica root: {ExceptionType} {HResult}", relativePath, ex.GetType().Name, ex.HResult);
        }
    }
}
