namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

internal static class PhotoTagExtensions
{
    public static TCollection AddIf<TCollection>(this TCollection tags, PhotoTag tag, bool hasTag)
        where TCollection : ICollection<PhotoTag>
    {
        if (hasTag)
        {
            tags.Add(tag);
        }

        return tags;
    }
}
