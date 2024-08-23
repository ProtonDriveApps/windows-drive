using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.App.FileSystem;

internal sealed class SharedWithMeFileRootDirectoryMaps<TId> : ISharedWithMeFileRootDirectoryMaps
{
    private static SharedWithMeFileRootDirectoryMaps<TId>? _instance;

    private readonly IReadOnlyDictionary<string, int> _sharedWithMeFilenamesToMappingIdMaps;

    public SharedWithMeFileRootDirectoryMaps(IReadOnlyDictionary<string, int> sharedWithMeFilenamesToMappingIdMaps)
    {
        _sharedWithMeFilenamesToMappingIdMaps = sharedWithMeFilenamesToMappingIdMaps;
    }

    public static SharedWithMeFileRootDirectoryMaps<TId> Empty =>
        _instance ??= new SharedWithMeFileRootDirectoryMaps<TId>(ImmutableDictionary<string, int>.Empty);

    public bool TryGetMappingIdFromSharedWithMeFileName(string fileName, [NotNullWhen(true)] out int? mappingId)
    {
        if (!_sharedWithMeFilenamesToMappingIdMaps.TryGetValue(fileName, out var id))
        {
            mappingId = null;
            return false;
        }

        mappingId = id;
        return true;
    }
}
