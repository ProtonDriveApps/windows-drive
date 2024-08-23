using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.EarlyAccess;

internal sealed class EarlyAccessService : IStartableService
{
    private readonly IRepository<UserSettings> _settingsRepository;
    private readonly Lazy<IEnumerable<IEarlyAccessStateAware>> _stateAware;
    private readonly ILogger<EarlyAccessService> _logger;

    private EarlyAccessStatus _status;

    public EarlyAccessService(
        IRepository<UserSettings> settingsRepository,
        Lazy<IEnumerable<IEarlyAccessStateAware>> stateAware,
        ILogger<EarlyAccessService> logger)
    {
        _settingsRepository = settingsRepository;
        _stateAware = stateAware;
        _logger = logger;
    }

    public EarlyAccessStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnStatusChanged(value);
        }
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsRepository.Get() ?? new UserSettings();

        if (settings.EarlyAccessEnabled)
        {
            _logger.LogInformation("Early access is enabled");
            Status = EarlyAccessStatus.Enabled;
        }
        else
        {
            _logger.LogInformation("Early access is disabled");
            Status = EarlyAccessStatus.Disabled;
        }

        return Task.CompletedTask;
    }

    private void OnStatusChanged(EarlyAccessStatus value)
    {
        foreach (var listener in _stateAware.Value)
        {
            listener.OnEarlyAccessStateChanged(value);
        }
    }
}
