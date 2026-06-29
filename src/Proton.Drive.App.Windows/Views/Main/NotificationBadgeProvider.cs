using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Proton.Drive.App.Update;
using Proton.Drive.App.Windows.Resources;
using Proton.Drive.App.Windows.Views.Shared.Notification;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Main;

internal sealed class NotificationBadgeProvider
    : ObservableObject, IUserStateAware, ISyncFoldersAware, IFeatureFlagsAware
{
    private readonly IScheduler _scheduler;
    private readonly NotificationBadge _newVersionNotificationBadge;
    private readonly NotificationBadge _syncFoldersFailureNotificationBadge;
    private readonly NotificationBadge _sharedWithMeFeatureDisabledNotificationBadge;
    private readonly NotificationBadge _updateRequiredNotificationBadge;
    private readonly NotificationBadge _warningLevel1QuotaNotificationBadge;
    private readonly NotificationBadge _warningLevel2QuotaNotificationBadge;
    private readonly NotificationBadge _exceededQuotaNotificationBadge;
    private readonly Dictionary<SyncFolderType, HashSet<string>> _failedSyncFoldersByType = [];

    private UserState? _user;
    private bool _sharingFeatureIsDisabled;

    public NotificationBadgeProvider(
        IUpdateService updateService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _scheduler = scheduler;
        updateService.StateChanged += OnUpdateServiceStateChanged;

        _updateRequiredNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_UpdateRequired_Description,
            NotificationBadgeSeverity.Alert);

        _newVersionNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_NewVersion_Description,
            NotificationBadgeSeverity.Warning);

        _syncFoldersFailureNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_SyncFoldersFailure_Description,
            NotificationBadgeSeverity.Warning);

        _sharedWithMeFeatureDisabledNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_SharingUnavailable_Description,
            NotificationBadgeSeverity.Warning);

        _exceededQuotaNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_ExceededQuota_Description,
            NotificationBadgeSeverity.Alert);

        _warningLevel2QuotaNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_ExceedingQuota_Description,
            NotificationBadgeSeverity.Alert);

        _warningLevel1QuotaNotificationBadge = new NotificationBadge(
            "!",
            Strings.Main_Sidebar_Notification_ExceedingQuota_Description,
            NotificationBadgeSeverity.Warning);
    }

    public NotificationBadge? MyComputerNotificationBadge
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public NotificationBadge? SharedWithMeNotificationBadge
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public NotificationBadge? PhotosNotificationBadge
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public NotificationBadge? SettingsNotificationBadge
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public NotificationBadge? UpdateNotificationBadge
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public NotificationBadge? QuotaNotificationBadge
    {
        get;
        private set => SetProperty(ref field, value);
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        _user = value.IsEmpty ? null : value;
        UpdateQuotaNotification();
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        Schedule(() => RefreshSyncFolderNotificationBadges(changeType, folder));
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        Schedule(() => RefreshSharedWithMeNotificationBadge(features));
    }

    private void RefreshSharedWithMeNotificationBadge(IReadOnlyDictionary<Feature, bool> features)
    {
        _sharingFeatureIsDisabled = features[Feature.DriveSharingDisabled] || features[Feature.DriveSharingEditingDisabled];
        SharedWithMeNotificationBadge = GetSharedWithMeNotificationBadge();
    }

    private NotificationBadge? GetSharedWithMeNotificationBadge()
    {
        if (_sharingFeatureIsDisabled)
        {
            return _sharedWithMeFeatureDisabledNotificationBadge;
        }

        // If the sharing feature happens to be reactivated
        // but some mappings failed to set up, the notification badge is kept but updated.
        return _failedSyncFoldersByType.TryGetValue(SyncFolderType.SharedWithMeItem, out var failures) && failures.Count > 0
            ? _syncFoldersFailureNotificationBadge
            : null;
    }

    private void RefreshSyncFolderNotificationBadges(SyncFolderChangeType changeType, SyncFolder folder)
    {
        if (folder.Type is not (
            SyncFolderType.HostDeviceFolder
            or SyncFolderType.AccountRoot
            or SyncFolderType.SharedWithMeItem))
        {
            return;
        }

        var folderFailedToSetup = changeType is not SyncFolderChangeType.Removed
            && folder.Status is MappingSetupStatus.Failed or MappingSetupStatus.PartiallySucceeded;

        if (!_failedSyncFoldersByType.TryGetValue(folder.Type, out var failedFolders))
        {
            if (!folderFailedToSetup)
            {
                return;
            }

            failedFolders = [];
            _failedSyncFoldersByType.Add(folder.Type, failedFolders);
        }

        if (folderFailedToSetup)
        {
            failedFolders.Add(folder.LocalPath);
            SetNotificationBadgeForFolderType(folder.Type, isVisible: true);
        }
        else
        {
            failedFolders.Remove(folder.LocalPath);
            SetNotificationBadgeForFolderType(folder.Type, isVisible: failedFolders.Count > 0);
        }

        return;

        void SetNotificationBadgeForFolderType(SyncFolderType folderType, bool isVisible)
        {
            switch (folderType)
            {
                case SyncFolderType.HostDeviceFolder:
                    MyComputerNotificationBadge = isVisible ? _syncFoldersFailureNotificationBadge : null;
                    break;

                case SyncFolderType.AccountRoot:
                    SettingsNotificationBadge = isVisible ? _syncFoldersFailureNotificationBadge : null;
                    break;

                case SyncFolderType.SharedWithMeItem:
                    SharedWithMeNotificationBadge = GetSharedWithMeNotificationBadge();
                    break;
            }
        }
    }

    private void UpdateQuotaNotification()
    {
        QuotaNotificationBadge = _user?.UserQuotaStatus switch
        {
            UserQuotaStatus.LimitExceeded => _exceededQuotaNotificationBadge,
            UserQuotaStatus.WarningLevel2Exceeded => _warningLevel2QuotaNotificationBadge,
            UserQuotaStatus.WarningLevel1Exceeded => _warningLevel1QuotaNotificationBadge,
            _ => null,
        };
    }

    private void OnUpdateServiceStateChanged(object? sender, UpdateState state)
    {
        if (state.UpdateRequired)
        {
            UpdateNotificationBadge = _updateRequiredNotificationBadge;
        }
        else if (state.IsReady)
        {
            UpdateNotificationBadge = _newVersionNotificationBadge;
        }
        else
        {
            UpdateNotificationBadge = null;
        }
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
