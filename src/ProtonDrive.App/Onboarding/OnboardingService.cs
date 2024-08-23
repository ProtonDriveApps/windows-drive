using System;
using System.Collections.Generic;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.Onboarding;

internal sealed class OnboardingService : IOnboardingService
{
    private readonly Lazy<IEnumerable<IOnboardingStateAware>> _onboardingStateAware;
    private readonly IRepository<OnboardingSettings> _settings;

    private OnboardingState _onboardingState;

    public OnboardingService(
        Lazy<IEnumerable<IOnboardingStateAware>> onboardingStateAware,
        IRepository<OnboardingSettings> settings)
    {
        _onboardingStateAware = onboardingStateAware;
        _settings = settings;

        RefreshOnboardingState();
    }

    public void SetSyncFolderSelectionCompleted()
    {
        var settings = _settings.Get() ?? new OnboardingSettings();
        settings.IsSyncFolderSelectionCompleted = true;
        _settings.Set(settings);

        RefreshOnboardingState();
    }

    public bool IsSyncFolderSelectionCompleted()
    {
        return _settings.Get()?.IsSyncFolderSelectionCompleted ?? false;
    }

    public void SetAccountRootFolderSelectionCompleted()
    {
        var settings = _settings.Get() ?? new OnboardingSettings();
        settings.IsAccountRootFolderSelectionCompleted = true;
        _settings.Set(settings);

        RefreshOnboardingState();
    }

    public bool IsAccountRootFolderSelectionCompleted()
    {
        return _settings.Get()?.IsAccountRootFolderSelectionCompleted ?? false;
    }

    public void SetUpgradeStorageStepCompleted()
    {
        var settings = _settings.Get() ?? new OnboardingSettings();
        settings.IsUpgradeStorageStepCompleted = true;
        _settings.Set(settings);
    }

    public bool IsUpgradeStorageStepCompleted()
    {
        return _settings.Get()?.IsUpgradeStorageStepCompleted ?? false;
    }

    private OnboardingState GetOnboardingState()
    {
        var settings = _settings.Get();

        if (settings is null)
        {
            return OnboardingState.NotStarted;
        }

        return settings switch
        {
            { IsAccountRootFolderSelectionCompleted: true, IsSyncFolderSelectionCompleted: true } => OnboardingState.Completed,
            { IsAccountRootFolderSelectionCompleted: false, IsSyncFolderSelectionCompleted: false } => OnboardingState.NotStarted,
            _ => OnboardingState.InProgress,
        };
    }

    private void RefreshOnboardingState()
    {
        _onboardingState = GetOnboardingState();
        OnOnboardingStateChanged();
    }

    private void OnOnboardingStateChanged()
    {
        foreach (var listener in _onboardingStateAware.Value)
        {
            listener.OnboardingStateChanged(_onboardingState);
        }
    }
}
