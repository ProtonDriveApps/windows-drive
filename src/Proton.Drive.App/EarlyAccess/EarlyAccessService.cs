using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Shared.Repository;

namespace Proton.Drive.App.EarlyAccess;

internal sealed class EarlyAccessService : IStartableService, IEarlyAccessService
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

    void IEarlyAccessService.SetEarlyAccessStatus(EarlyAccessStatus status)
    {
        if (Status == status)
        {
            return;
        }

        SaveEarlyAccessEnabled(status);
    }

    private void SaveEarlyAccessEnabled(EarlyAccessStatus status)
    {
        var settings = _settingsRepository.Get() ?? new UserSettings();
        settings.EarlyAccessEnabled = status == EarlyAccessStatus.Enabled;
        _settingsRepository.Set(settings);

        Status = status;
        _logger.LogInformation(settings.EarlyAccessEnabled ? "Early access is now enabled" : "Early access is now disabled");
    }

    private void OnStatusChanged(EarlyAccessStatus value)
    {
        foreach (var listener in _stateAware.Value)
        {
            listener.OnEarlyAccessStateChanged(value);
        }
    }
}
