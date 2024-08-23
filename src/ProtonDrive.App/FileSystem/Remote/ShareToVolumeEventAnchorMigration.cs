using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.FileSystem.Remote;

public sealed class ShareToVolumeEventAnchorMigration
{
    private readonly IPropertyRepository _propertyRepository;

    public ShareToVolumeEventAnchorMigration(IPropertyRepository propertyRepository)
    {
        _propertyRepository = propertyRepository;
    }

    public bool IsRequired()
    {
        return GetShareToAnchorMapping().Any();
    }

    public bool TryApply(IReadOnlyDictionary<string, string> shareToVolumeMapping)
    {
        var volumeToAnchorMapping = GetVolumeToAnchorMapping(shareToVolumeMapping);

        if (volumeToAnchorMapping.Values.Any(anchorId => anchorId == null))
        {
            // Migration not possible
            return false;
        }

        // Add volume event anchor IDs
        foreach (var (volumeId, volumeAnchorId) in volumeToAnchorMapping)
        {
            _propertyRepository.Set(RemoteDecoratedEventLogClientFactory.VolumeEventAnchorIdPrefix + volumeId, volumeAnchorId);
        }

        // Delete obsolete share event anchor IDs
        foreach (var shareId in GetShareToAnchorMapping().Keys)
        {
            _propertyRepository.Set<string?>(RemoteDecoratedEventLogClientFactory.ShareEventAnchorIdPrefix + shareId, null);
        }

        return true;
    }

    public void ForceMigration()
    {
        foreach (var key in GetShareBasedEventKeys())
        {
            // Delete key
            _propertyRepository.Set<string?>(key, null);
        }
    }

    private IEnumerable<string> GetShareBasedEventKeys()
    {
        return _propertyRepository.GetKeys()
            .Where(key => key.StartsWith(RemoteDecoratedEventLogClientFactory.ShareEventAnchorIdPrefix));
    }

    private IReadOnlyDictionary<string, string> GetShareToAnchorMapping()
    {
        return GetShareBasedEventKeys().ToDictionary(
                key => key[RemoteDecoratedEventLogClientFactory.ShareEventAnchorIdPrefix.Length..],
                key => _propertyRepository.Get<string>(key) ?? string.Empty)
            .AsReadOnly();
    }

    private IReadOnlyDictionary<string, string?> GetVolumeToAnchorMapping(IReadOnlyDictionary<string, string> shareToVolumeMapping)
    {
        var volumeToAnchorMapping = new Dictionary<string, string?>();

        foreach (var (shareId, shareAnchorId) in GetShareToAnchorMapping())
        {
            if (!shareToVolumeMapping.TryGetValue(key: shareId, out var volumeId) || string.IsNullOrEmpty(volumeId))
            {
                continue;
            }

            if (!volumeToAnchorMapping.TryGetValue(volumeId, out var volumeAnchorId))
            {
                volumeToAnchorMapping.Add(volumeId, shareAnchorId);
            }
            else if (volumeAnchorId != null && volumeAnchorId != shareAnchorId)
            {
                // The several different event IDs are found on the same volume, we cannot safely choose which one is the oldest
                volumeToAnchorMapping[volumeId] = null;
            }
        }

        return volumeToAnchorMapping;
    }
}
