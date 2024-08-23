using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Mapping;

namespace ProtonDrive.App.Windows.Views.Main.Computers;

internal sealed class DeviceViewModel : ObservableObject, IEquatable<Device>
{
    private readonly Device _dataItem;
    private MappingErrorCode _setupErrorCode;
    private MappingSetupStatus _setupStatus;

    public DeviceViewModel(Device dataItem)
    {
        _dataItem = dataItem;
        _setupErrorCode = MappingErrorCode.None;
        _setupStatus = MappingSetupStatus.None;
    }

    public string ShareId => _dataItem.ShareId;

    public string VolumeId => _dataItem.VolumeId;

    public string Name => _dataItem.Name;

    public bool ExistsOnRemote => _dataItem.Id.Length > 0;

    public MappingErrorCode SetupErrorCode
    {
        get => _setupErrorCode;
        set
        {
            SetProperty(ref _setupErrorCode, value);
        }
    }

    public MappingSetupStatus SetupStatus
    {
        get => _setupStatus;
        set => SetProperty(ref _setupStatus, value);
    }

    public bool Equals(Device? dataItem)
    {
        return dataItem is not null && _dataItem == dataItem;
    }

    internal void DataItemUpdated()
    {
        OnPropertyChanged(nameof(Name));
    }
}
