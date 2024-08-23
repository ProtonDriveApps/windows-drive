using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoreLinq;
using ProtonDrive.App.Authentication;
using ProtonDrive.Client;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Features;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Features;

public sealed class FeatureService : ISessionStateAware, IFeatureFlagProvider
{
    // If the API is not returning a feature flag/kill switch, we can safely consider it as "disabled".
    private const bool FallbackValueForMissingFeatureFlag = false;

    private readonly IFeatureApiClient _featureApiClient;
    private readonly ILogger<FeatureService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly TimeSpan _period;
    private readonly Lazy<IEnumerable<IFeatureFlagsAware>> _featureFlagsAware;
    private readonly Func<TimeSpan, IPeriodicTimer> _periodicTimerFactory;

    private IReadOnlyDictionary<Feature, bool> _cachedFeatureFlags = new Dictionary<Feature, bool>();

    private IPeriodicTimer _timer;
    private Task? _timerTask;
    private Task? _firstRefreshFeaturesTask;
    private bool _featuresFetchedAtLeastOnce;

    public FeatureService(
        DriveApiConfig config,
        IFeatureApiClient featureApiClient,
        Lazy<IEnumerable<IFeatureFlagsAware>> featureFlagsAware,
        Func<TimeSpan, IPeriodicTimer> periodicTimerFactory,
        ILogger<FeatureService> logger)
    {
        _featureApiClient = featureApiClient;
        _logger = logger;
        _featureFlagsAware = featureFlagsAware;
        _periodicTimerFactory = periodicTimerFactory;
        _period = config.FeaturesUpdateInterval.RandomizedWithDeviation(0.2);
        _timer = periodicTimerFactory.Invoke(_period);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        if (value.Status is SessionStatus.Started)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    public async Task<bool> IsEnabledAsync(Feature feature, CancellationToken cancellationToken)
    {
        await EnsureFeaturesAreRetrievedAtLeastOnce(cancellationToken).ConfigureAwait(false);

        return IsEnabled(feature);
    }

    private async Task EnsureFeaturesAreRetrievedAtLeastOnce(CancellationToken cancellationToken)
    {
        if (_featuresFetchedAtLeastOnce)
        {
            return;
        }

        if (_firstRefreshFeaturesTask is null)
        {
            return;
        }

        if (_firstRefreshFeaturesTask.Status is not TaskStatus.RanToCompletion)
        {
            await _firstRefreshFeaturesTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsEnabled(Feature feature)
    {
        return _cachedFeatureFlags.TryGetValue(feature, out var enabled) && enabled;
    }

    private void Start()
    {
        if (_timerTask is not null)
        {
            return; // Task already started
        }

        _logger.LogInformation("Feature service is about to start...");
        _timer = _periodicTimerFactory.Invoke(_period);
        _timerTask = GetTimerTaskAsync(_cancellationHandle.Token);
    }

    private void Stop()
    {
        if (_timerTask is null)
        {
            return;
        }

        _logger.LogInformation("Feature service is about to stop...");
        _timerTask = null;
        _firstRefreshFeaturesTask = null;
        _cancellationHandle.Cancel();
        _timer.Dispose();
        _cachedFeatureFlags = ImmutableDictionary<Feature, bool>.Empty;
        _featuresFetchedAtLeastOnce = false;
    }

    private async Task GetTimerTaskAsync(CancellationToken cancellationToken)
    {
        try
        {
            do
            {
                var task = RefreshFeatureFlagsAsync(cancellationToken);
                _firstRefreshFeaturesTask ??= task;

                await task.ConfigureAwait(false);
                _featuresFetchedAtLeastOnce = true;
            }
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            /* Do nothing */
        }
    }

    private async Task RefreshFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var featureListResponse = await _featureApiClient.GetFeaturesAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            var latestFeatureFlags = featureListResponse.FeatureFlags
                .Select(
                    x => Enum.TryParse(x.Name, out Feature featureFlag)
                        ? new (Feature Feature, bool IsEnabled)?((featureFlag, x.Enabled))
                        : null)
                .Where(x => x is not null)
                .ToDictionary(x => x!.Value.Feature, y => y!.Value.IsEnabled);

            var featureFlagComparisons = latestFeatureFlags.FullJoin(
                _cachedFeatureFlags,
                x => x.Key,
                y => y.Key,
                x => (Feature: x.Key, IsEnabled: x.Value, WasEnabled: FallbackValueForMissingFeatureFlag),
                y => (Feature: y.Key, IsEnabled: FallbackValueForMissingFeatureFlag, WasEnabled: y.Value),
                (x, y) => (Feature: x.Key, IsEnabled: x.Value, WasEnabled: y.Value)).ToList();

            var changedFeatureFlags = featureFlagComparisons.Where(x => x.IsEnabled != x.WasEnabled);

            if (!changedFeatureFlags.Any())
            {
                return; // Nothing has changed
            }

            OnFeatureFlagsChanged(featureFlagComparisons.Select(x => (x.Feature, x.IsEnabled)).ToList());

            _cachedFeatureFlags = latestFeatureFlags.AsReadOnly();
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Failed to refresh features flags: {ErrorMessage}", ex.CombinedMessage());
        }
    }

    private void OnFeatureFlagsChanged(IReadOnlyCollection<(Feature Feature, bool IsEnabled)> features)
    {
        foreach (var listener in _featureFlagsAware.Value)
        {
            listener.OnFeatureFlagsChanged(features);
        }
    }
}
