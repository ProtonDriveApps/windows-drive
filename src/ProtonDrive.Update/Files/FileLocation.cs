using System;
using System.IO;
using System.Web;

namespace ProtonDrive.Update.Files;

/// <summary>
/// Represents downloadable file location and translates file URL into file path on disk.
/// </summary>
internal class FileLocation
{
    private readonly string _folderPath;

    public FileLocation(string folderPath)
    {
        _folderPath = folderPath;
    }

    public string GetPath(string url)
    {
        var uri = new Uri(url);
        // Calling GetFileName a second time after decoding to avoid potential security issues
        var filename = Path.GetFileName(HttpUtility.UrlDecode(Path.GetFileName(url)));

        return Path.Combine(_folderPath, filename);
    }
}
