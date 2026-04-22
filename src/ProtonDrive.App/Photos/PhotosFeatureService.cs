using Microsoft.Extensions.Logging;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Services;
using ProtonDrive.App.Volumes;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Photos;

internal sealed class PhotosFeatureService : IStartableService, IStoppableService, IMainVolumeStateAware, IPhotoVolumeStateAware, IPhotosOnboardingStateAware, IFeatureFlagsAware
{
    private readonly Lazy<IEnumerable<IPhotosFeatureStateAware>> _photosFeatureStateAware;
    private readonly ILogger<PhotosFeatureService> _logger;

    private readonly CoalescingAction _stateChangeHandler;

    private PhotosFeatureState _state = PhotosFeatureState.Idle;
    private VolumeState _mainVolumeState = VolumeState.Idle;
    private VolumeState _photoVolumeState = VolumeState.Idle;
    private OnboardingStatus _onboardingStatus = OnboardingStatus.NotStarted;
    private bool _featureIsReadOnly;
    private bool _isStopping;

    public PhotosFeatureService(Lazy<IEnumerable<IPhotosFeatureStateAware>> photosFeatureStateAware, ILogger<PhotosFeatureService> logger)
    {
        _photosFeatureStateAware = photosFeatureStateAware;
        _logger = logger;

        _stateChangeHandler = _logger.GetCoalescingActionWithExceptionsLogging(HandleExternalStateChange, nameof(PhotosFeatureService));
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        ScheduleExternalStateChangeHandling();

        return Task.CompletedTask;
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(PhotosFeatureService)} is stopping");

        _isStopping = true;
        _stateChangeHandler.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogDebug($"{nameof(PhotosFeatureService)} stopped");
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        _mainVolumeState = value;
        ScheduleExternalStateChangeHandling();
    }

    void IPhotoVolumeStateAware.OnPhotoVolumeStateChanged(VolumeState value)
    {
        _photoVolumeState = value;
        ScheduleExternalStateChangeHandling();
    }

    void IPhotosOnboardingStateAware.OnPhotosOnboardingStateChanged(OnboardingStatus value)
    {
        _onboardingStatus = value;
        ScheduleExternalStateChangeHandling();
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _featureIsReadOnly = features[Feature.DrivePhotosUploadDisabled] || features[Feature.DriveAlbumsDisabled];
        ScheduleExternalStateChangeHandling();
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _stateChangeHandler.WaitForCompletionAsync();
    }

    private void ScheduleExternalStateChangeHandling()
    {
        if (_isStopping)
        {
            return;
        }

        _stateChangeHandler.Run();
    }

    private void HandleExternalStateChange()
    {
        if (_isStopping)
        {
            return;
        }

        if (_featureIsReadOnly)
        {
            SetState(PhotosFeatureStatus.ReadOnly);
            return;
        }

        if (_mainVolumeState.Status is not VolumeStatus.Ready)
        {
            SetState(PhotosFeatureStatus.Idle);
            return;
        }

        if (_onboardingStatus is not OnboardingStatus.Completed)
        {
            SetState(PhotosFeatureStatus.Onboarding);
            return;
        }

        if (_photoVolumeState.Status is VolumeStatus.Ready)
        {
            SetState(PhotosFeatureStatus.Ready);
            return;
        }

        SetState(PhotosFeatureStatus.SettingUp);
    }

    private void SetState(PhotosFeatureStatus status)
    {
        var state = new PhotosFeatureState(status);
        if (_state == state)
        {
            return;
        }

        _state = state;
        OnStateChanged(state);
    }

    private void OnStateChanged(PhotosFeatureState value)
    {
        _logger.LogInformation("Photos feature state changed to {Status}", value.Status);

        foreach (var listener in _photosFeatureStateAware.Value)
        {
            listener.OnPhotosFeatureStateChanged(value);
        }
    }
}
