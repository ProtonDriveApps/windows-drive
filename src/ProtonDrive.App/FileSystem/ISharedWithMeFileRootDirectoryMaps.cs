using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.App.FileSystem;

internal interface ISharedWithMeFileRootDirectoryMaps
{
    public bool TryGetMappingIdFromSharedWithMeFileName(string fileName, [NotNullWhen(true)] out int? mappingId);
}
