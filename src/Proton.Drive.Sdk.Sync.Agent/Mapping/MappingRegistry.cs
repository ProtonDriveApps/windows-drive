using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal sealed class MappingRegistry : IStartableService, IStoppableService, IMappingRegistry
{
    private readonly IRepository<MappingSettings> _repository;
    private readonly Lazy<IEnumerable<IMappingsAware>> _mappingAwareObjects;
    private readonly ILogger<MappingRegistry> _logger;

    private readonly List<RemoteToLocalMapping> _activeMappings = [];
    private readonly List<RemoteToLocalMapping> _deletedMappings = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Lazy<Task> _initialization;

    private int _latestId;
    private volatile bool _stopping;

    public MappingRegistry(
        IRepository<MappingSettings> mappingRepository,
        Lazy<IEnumerable<IMappingsAware>> mappingAwareObjects,
        ILogger<MappingRegistry> logger)
    {
        _repository = mappingRepository;
        _mappingAwareObjects = mappingAwareObjects;
        _logger = logger;

        _initialization = new Lazy<Task>(() => Schedule(LoadData, CancellationToken.None));
    }

    public async Task<IUpdatableMappings> GetMappingsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync().ConfigureAwait(false);

        var disposable = await _semaphore.LockAsync(cancellationToken).ConfigureAwait(false);

        return new UpdatableMappings(this, disposable);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!_initialization.Value.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException("Mappings not loaded");
        }

        return Schedule(SaveMappings, cancellationToken);
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        InitializeAsync();

        return Task.CompletedTask;
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(MappingRegistry)} is stopping");
        _stopping = true;

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(MappingRegistry)} stopped");
    }

    internal async Task WaitForCompletionAsync()
    {
        // Wait for current internal task to complete
        using (await _semaphore.LockAsync(CancellationToken.None).ConfigureAwait(false))
        {
        }
    }

    private Task InitializeAsync()
    {
        return _initialization.Value;
    }

    private void LoadData()
    {
        _logger.LogDebug("Loading mappings...");

        LoadMappings();
        NotifyMappingsChanged();

        _logger.LogInformation(
            "Mappings loaded: {NumberOfActiveMappings} active, {NumberOfDeletedMappings} deleted, latest ID {LatestId}",
            _activeMappings.Count,
            _deletedMappings.Count,
            _latestId);
    }

    private void LoadMappings()
    {
        var settings = _repository.Get() ?? new MappingSettings();
        _latestId = settings.LatestId;

        foreach (var mapping in settings.Mappings)
        {
            if (!mapping.TypeIsDefined())
            {
                continue;
            }

            switch (mapping.Status)
            {
                case MappingStatus.New:
                case MappingStatus.Complete:
                    _activeMappings.Add(mapping);
                    break;

                case MappingStatus.Deleted:
                case MappingStatus.TornDown:
                    _deletedMappings.Add(mapping);
                    break;

                default:
                    throw new InvalidEnumArgumentException(nameof(mapping.Status), (int)mapping.Status, typeof(MappingStatus));
            }
        }
    }

    private void AddMapping(RemoteToLocalMapping mapping)
    {
        if (_deletedMappings.Contains(mapping))
        {
            throw new InvalidOperationException("Deleted mapping cannot be made active");
        }

        if (_activeMappings.Contains(mapping))
        {
            throw new InvalidOperationException("Mapping is already added");
        }

        if (mapping.Status is not MappingStatus.New)
        {
            throw new InvalidOperationException($"New mapping status must be {MappingStatus.New}");
        }

        mapping.Id = GetNextId();
        _activeMappings.Add(mapping);

        _logger.LogInformation("Added mapping {Id} ({Type})", mapping.Id, mapping.Type);
    }

    private void DeleteMapping(RemoteToLocalMapping mapping)
    {
        if (_deletedMappings.Contains(mapping))
        {
            throw new InvalidOperationException("Mapping is already deleted");
        }

        if (!_activeMappings.Remove(mapping))
        {
            throw new InvalidOperationException("Mapping does not exist");
        }

        mapping.Status = MappingStatus.Deleted;
        _deletedMappings.Add(mapping);

        _logger.LogInformation("Deleted mapping {Id} ({Type})", mapping.Id, mapping.Type);
    }

    private void RemoveMapping(RemoteToLocalMapping mapping)
    {
        if (mapping.Status is not MappingStatus.TornDown)
        {
            throw new InvalidOperationException($"Only {MappingStatus.TornDown} mapping can be removed");
        }

        if (!_deletedMappings.Remove(mapping))
        {
            throw new InvalidOperationException("Mapping does not exist");
        }

        _logger.LogInformation("Removed mapping {Id} ({Type})", mapping.Id, mapping.Type);
    }

    private void ClearMappings()
    {
        _logger.LogInformation("Started clearing mappings");

        _activeMappings.Clear();
        _deletedMappings.Clear();
        _latestId = 0;

        _logger.LogInformation("Finished clearing mappings");
    }

    private void SaveMappings()
    {
        _repository.Set(new MappingSettings
        {
            Mappings = [.. _activeMappings, .. _deletedMappings],
            LatestId = _latestId,
        });
    }

    private int GetNextId() => ++_latestId;

    private void NotifyMappingsChanged()
    {
        if (_stopping)
        {
            return;
        }

        OnMappingsChanged(
            _activeMappings.ToArray().AsReadOnly(),
            _deletedMappings.ToArray().AsReadOnly());
    }

    private void OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        foreach (var listener in _mappingAwareObjects.Value)
        {
            listener.OnMappingsChanged(activeMappings, deletedMappings);
        }
    }

    private async Task Schedule(Action action, CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        using (await _semaphore.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_stopping)
            {
                return;
            }

            action.Invoke();
        }
    }

    private class UpdatableMappings : IUpdatableMappings
    {
        private readonly MappingRegistry _mappingRegistry;
        private readonly IDisposable _disposable;

        private bool _isDirty;

        public UpdatableMappings(MappingRegistry mappingRegistry, IDisposable disposable)
        {
            _mappingRegistry = mappingRegistry;
            _disposable = disposable;
        }

        public IReadOnlyCollection<RemoteToLocalMapping> GetActive()
        {
            return _mappingRegistry._activeMappings.ToArray().AsReadOnly();
        }

        public void Add(RemoteToLocalMapping mapping)
        {
            _mappingRegistry.AddMapping(mapping);

            _isDirty = true;
        }

        public void Update(RemoteToLocalMapping mapping)
        {
            _isDirty = true;
        }

        public void Delete(RemoteToLocalMapping mapping)
        {
            _mappingRegistry.DeleteMapping(mapping);

            _isDirty = true;
        }

        public void Remove(RemoteToLocalMapping mapping)
        {
            _mappingRegistry.RemoveMapping(mapping);

            _isDirty = true;
        }

        public void Clear()
        {
            _mappingRegistry.ClearMappings();

            _isDirty = true;
        }

        public void SaveAndNotify()
        {
            if (!_isDirty)
            {
                return;
            }

            _mappingRegistry.SaveMappings();
            _mappingRegistry.NotifyMappingsChanged();

            _isDirty = false;
        }

        public void Dispose()
        {
            SaveAndNotify();

            _disposable.Dispose();
        }
    }
}
