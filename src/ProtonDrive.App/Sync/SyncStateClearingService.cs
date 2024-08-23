using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Sync;

/// <summary>
/// Clears synchronization state on switching user account.
/// </summary>
internal sealed class SyncStateClearingService : IAccountSwitchingHandler
{
    private readonly AppConfig _appConfig;
    private readonly ILogger<SyncStateClearingService> _logger;

    public SyncStateClearingService(AppConfig appConfig, ILogger<SyncStateClearingService> logger)
    {
        _appConfig = appConfig;
        _logger = logger;
    }

    Task<bool> IAccountSwitchingHandler.HandleAccountSwitchingAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ClearSyncState(cancellationToken));
    }

    private bool ClearSyncState(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogInformation("Started clearing synchronization state");

            UnsafeDeleteSyncState();

            _logger.LogInformation("Finished clearing synchronization state");

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError("Failed to clear synchronization state: {Error}", ex.Message);
        }

        return false;

        void UnsafeDeleteSyncState()
        {
            foreach (var fileToDelete in Directory.EnumerateFiles(_appConfig.AppDataPath, "*.sqlite*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                File.Delete(fileToDelete);
            }
        }
    }
}
