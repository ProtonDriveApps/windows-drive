using System;
using System.Linq;
using System.Runtime.InteropServices;
using ProtonDrive.App.Windows.Interop;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal static class KnownFolders
{
    private const string DocumentsGuidString = "FDD39AD0-238F-46AF-ADB4-6C85480369C7";
    private const string PicturesGuidString = "33E28130-4E1E-4676-835A-98395C3BC3BB";
    private const string VideosGuidString = "18989B1D-99B5-455B-841C-AB7C74E4DDFC";
    private const string MusicGuidString = "4BD8D571-6D19-48D3-BE97-422220080E43";
    private const string DownloadsGuidString = "374DE290-123F-4565-9164-39C4925E467B";
    private const string DesktopGuidString = "B4BFCC3A-DB2C-424C-B029-7FE99A87C641";

    static KnownFolders()
    {
        Desktop = Guid.Parse(DesktopGuidString);
        Documents = Guid.Parse(DocumentsGuidString);
        Downloads = Guid.Parse(DownloadsGuidString);
        Music = Guid.Parse(MusicGuidString);
        Pictures = Guid.Parse(PicturesGuidString);
        Videos = Guid.Parse(VideosGuidString);

        var ids = new[] { Desktop, Documents, Downloads, Music, Pictures, Videos, };

        IdsByPath = ids
            .Select(id => (Id: id, Path: GetPath(id)))
            .Where(folder => folder.Path is not null)
            .ToLookup(folder => folder.Path!, folder => folder.Id);
    }

    public static Guid Documents { get; }
    public static Guid Pictures { get; }
    public static Guid Videos { get; }
    public static Guid Music { get; }
    public static Guid Downloads { get; }
    public static Guid Desktop { get; }

    public static ILookup<string, Guid> IdsByPath { get; }

    private static string? GetPath(Guid knownFolderGuid)
    {
        try
        {
            return Shell32.SHGetKnownFolderPath(knownFolderGuid, 0);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            return null;
        }
    }
}
