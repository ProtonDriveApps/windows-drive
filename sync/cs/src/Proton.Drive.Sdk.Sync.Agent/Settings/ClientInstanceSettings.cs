using System.Security.Cryptography;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Repository;

namespace Proton.Drive.Sdk.Sync.Agent.Settings;

public sealed class ClientInstanceSettings
{
    private readonly Version _currentVersion;

    private readonly Lazy<ClientInstanceSettingsState> _state;

    public ClientInstanceSettings(IRepositoryFactory repositoryFactory, AppConfig appConfig)
    {
        _currentVersion = appConfig.AppVersion;

        _state = new Lazy<ClientInstanceSettingsState>(() => LoadOrCreateState(repositoryFactory));
    }

    public string ClientInstanceId => _state.Value.ClientInstanceId;

    public double RolloutEligibilityThreshold => _state.Value.RolloutEligibilityThreshold;

    public DateTimeOffset? LastAutoUpdateTime => _state.Value.LastAutoUpdateTime;

    public void SetLastAutoUpdateTime(DateTime time)
    {
        var settings = _state.Value.Repository.Get() ?? new ClientInstanceSettingsDto();
        settings = settings with { LastAutoUpdateTime = time };
        _state.Value.Repository.Set(settings);
        _state.Value.LastAutoUpdateTime = time;
    }

    private static double GenerateRolloutEligibilityThreshold()
    {
        return (double)RandomNumberGenerator.GetInt32(int.MaxValue) / int.MaxValue;
    }

    private ClientInstanceSettingsState LoadOrCreateState(IRepositoryFactory repositoryFactory)
    {
        var repository = repositoryFactory.GetRepository<ClientInstanceSettingsDto>("ClientInstanceSettings.json");

        var requiresSaving = false;

        var dto = repository.Get() ?? new ClientInstanceSettingsDto();

        string clientInstanceId;
        if (dto.ClientInstanceId is null)
        {
            clientInstanceId = Guid.NewGuid().ToString();
            requiresSaving = true;
        }
        else
        {
            clientInstanceId = dto.ClientInstanceId;
        }

        double rolloutEligibilityThreshold;
        if (dto.RolloutEligibilityThreshold is null
            || dto.RolloutEligibilityThresholdVersion is null
            || dto.RolloutEligibilityThresholdVersion < _currentVersion)
        {
            rolloutEligibilityThreshold = GenerateRolloutEligibilityThreshold();
            requiresSaving = true;
        }
        else
        {
            rolloutEligibilityThreshold = dto.RolloutEligibilityThreshold.Value;
        }

        var state = new ClientInstanceSettingsState(repository, clientInstanceId, rolloutEligibilityThreshold, dto.LastAutoUpdateTime);

        if (requiresSaving)
        {
            repository.Set((state, _currentVersion));
        }

        return state;
    }

    private sealed record ClientInstanceSettingsState(
        IRepository<ClientInstanceSettingsDto> Repository,
        string ClientInstanceId,
        double RolloutEligibilityThreshold,
        DateTimeOffset? LastAutoUpdateTime)
    {
        public DateTimeOffset? LastAutoUpdateTime { get; set; } = LastAutoUpdateTime;
    }

    private sealed record ClientInstanceSettingsDto(
        string? ClientInstanceId = null,
        double? RolloutEligibilityThreshold = null,
        Version? RolloutEligibilityThresholdVersion = null,
        DateTimeOffset? LastAutoUpdateTime = null)
    {
        public static implicit operator ClientInstanceSettingsDto(
            (ClientInstanceSettingsState State, Version CurrentVersion) x)
            => new(
                x.State.ClientInstanceId,
                x.State.RolloutEligibilityThreshold,
                x.CurrentVersion,
                x.State.LastAutoUpdateTime);
    }
}
