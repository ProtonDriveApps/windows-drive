using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Proton.Drive.App.Windows.Views.Shared;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class FolderViewModel : ObservableObject, IEquatable<SyncFolder>, IMappingStatusViewModel
{
    private bool _isStorageOptimizationEnabled;
    private MappingSetupStatus _status;
    private MappingErrorCode _errorCode;

    public FolderViewModel(
        SyncFolder syncFolder,
        string name,
        ImageSource? icon)
    {
        DataItem = syncFolder;
        Name = name;
        Icon = icon;
        Update();
    }

    public string Path => DataItem.LocalPath;
    public string Name { get; }
    public ImageSource? Icon { get; }

    public bool IsStorageOptimizationEnabled
    {
        get => _isStorageOptimizationEnabled;
        private set => SetProperty(ref _isStorageOptimizationEnabled, value);
    }

    public MappingSetupStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public MappingErrorCode ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public MappingErrorRenderingMode RenderingMode => MappingErrorRenderingMode.IconAndText;

    internal SyncFolder DataItem { get; }

    internal StorageOptimizationStatus StorageOptimizationStatus { get; private set; }

    public void Update()
    {
        IsStorageOptimizationEnabled = DataItem.IsStorageOptimizationEnabled;
        Status = DataItem.Status;
        ErrorCode = DataItem.ErrorCode;
        StorageOptimizationStatus = DataItem.StorageOptimizationStatus;
    }

    public bool Equals(SyncFolder? other)
    {
        return other is not null && DataItem == other;
    }
}
