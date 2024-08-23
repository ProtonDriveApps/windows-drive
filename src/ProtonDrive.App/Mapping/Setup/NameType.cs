namespace ProtonDrive.App.Mapping.Setup;

public enum NameType
{
    /// <summary>
    /// Append the suffix at the end of the name.
    /// <example>
    /// For a folder named "MyFolder", with suffix number 1, the resulting name would be "MyFolder (1)".
    /// </example>
    /// </summary>
    Folder,

    /// <summary>
    /// Appends the suffix before the file extension if present.
    /// <example>
    /// For a file named "Document.txt", with suffix number 1, the resulting name would be "Document (1).txt".
    /// </example>
    /// </summary>
    File,
}
