using System;
using System.Security.Cryptography;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.Settings;

public sealed class ClientInstanceSettings
{
    private readonly object _synchronizationLock = new();
    private readonly IRepository<ClientInstanceSettingsDto> _repository;
    private readonly Version _currentVersion;

    private Lazy<ClientInstanceSettingsState> _state;

    public ClientInstanceSettings(IRepositoryFactory repositoryFactory, AppConfig appConfig)
    {
        _currentVersion = appConfig.AppVersion;
        _repository = repositoryFactory.GetRepository<ClientInstanceSettingsDto>("ClientInstanceSettings.json");

        _state = new(LoadOrCreateState);
    }

    public string ClientInstanceId => _state.Value.ClientInstanceId;

    public double RolloutEligibilityThreshold => _state.Value.RolloutEligibilityThreshold;

    public (DateTimeOffset? Time, Version? Version) LastSanitization
    {
        get => (_state.Value.LastSanitizationTime, _state.Value.LastSanitizationVersion);
        set => ApplyStateTransformation((state, value) => state with { LastSanitizationTime = value.Time, LastSanitizationVersion = value.Version }, value);
    }

    private static double GenerateRolloutEligibilityThreshold()
    {
        return (double)RandomNumberGenerator.GetInt32(int.MaxValue) / int.MaxValue;
    }

    private void ApplyStateTransformation<TValue>(Func<ClientInstanceSettingsState, TValue, ClientInstanceSettingsState> transform, TValue value)
    {
        lock (_synchronizationLock)
        {
            var newState = transform.Invoke(_state.Value, value);

            _state = new Lazy<ClientInstanceSettingsState>(newState);

            _repository.Set((newState, _currentVersion));
        }
    }

    private ClientInstanceSettingsState LoadOrCreateState()
    {
        var requiresSaving = false;

        var dto = _repository.Get() ?? new ClientInstanceSettingsDto();

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

        var state = new ClientInstanceSettingsState(clientInstanceId, rolloutEligibilityThreshold, dto.LastSanitizationTime, dto.LastSanitizationVersion);

        if (requiresSaving)
        {
            _repository.Set((state, _currentVersion));
        }

        return state;
    }

    private sealed record ClientInstanceSettingsState(
        string ClientInstanceId,
        double RolloutEligibilityThreshold,
        DateTimeOffset? LastSanitizationTime,
        Version? LastSanitizationVersion);

    private sealed record ClientInstanceSettingsDto(
        string? ClientInstanceId = null,
        double? RolloutEligibilityThreshold = null,
        Version? RolloutEligibilityThresholdVersion = null,
        DateTimeOffset? LastSanitizationTime = null,
        Version? LastSanitizationVersion = null)
    {
        public static implicit operator ClientInstanceSettingsDto((ClientInstanceSettingsState State, Version CurrentVersion) x)
            => new(
                x.State.ClientInstanceId,
                x.State.RolloutEligibilityThreshold,
                x.CurrentVersion,
                x.State.LastSanitizationTime,
                x.CurrentVersion);
    }
}
