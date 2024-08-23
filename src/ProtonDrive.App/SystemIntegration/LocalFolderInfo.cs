using System;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.App.SystemIntegration;

public sealed record LocalFolderInfo
{
    private readonly LocalVolumeInfo? _volumeInfo;

    public long Id { get; init; }

    public LocalVolumeInfo VolumeInfo
    {
        get => _volumeInfo ?? throw new ArgumentNullException(nameof(VolumeInfo));
        init => _volumeInfo = value;
    }
}
