namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

public static class GoogleTakeoutPaths
{
    private const string TakeoutFolderName = "Takeout";

    /// <summary>
    /// Detects whether a path originates from a Google Takeout export by checking for the presence
    /// of the <c>Takeout</c> folder, which Google always names in English regardless of the export language.
    /// </summary>
    public static bool IsFromGoogleTakeoutImport(string filePath)
    {
        return filePath.Contains($"{Path.DirectorySeparatorChar}{TakeoutFolderName}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }
}
