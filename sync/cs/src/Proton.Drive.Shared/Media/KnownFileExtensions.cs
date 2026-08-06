using System.Collections.Frozen;

namespace Proton.Drive.Shared.Media;

public sealed class KnownFileExtensions
{
    public static readonly FrozenSet<string> JpegExtensions = new HashSet<string>
    {
        ".jfif",
        ".jif",
        ".jpe",
        ".jpeg",
        ".jpg",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> WebPImageExtensions = new HashSet<string>
    {
        ".webp",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> OtherImageExtensions = new HashSet<string>
    {
        ".apng",
        ".bmp",
        ".gif",
        ".heic",
        ".ico",
        ".png",
        ".tif",
        ".tiff",
        ".vdnMicrosoftIcon",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> RawImageExtensions = new HashSet<string>
    {
        ".3fr",
        ".arw",
        ".cr2",
        ".cr3",
        ".crw",
        ".dcr",
        ".dcraw",
        ".dng",
        ".erf",
        ".fff",
        ".iiq",
        ".k25",
        ".kdc",
        ".mef",
        ".mos",
        ".mrw",
        ".nef",
        ".nrw",
        ".orf",
        ".pef",
        ".ptx",
        ".sr2",
        ".srf",
        ".srw",
        ".raf",
        ".raw",
        ".rw2",
        ".rwl",
        ".rwz",
        ".x3f",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> VectorImageExtensions = new HashSet<string>
    {
        ".svg",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> VideoExtensions = new HashSet<string>
    {
        ".3g2",
        ".3gp",
        ".3gpp",
        ".asf",
        ".asx",
        ".avi",
        ".dvb",
        ".f4v",
        ".fli",
        ".flv",
        ".fvt",
        ".h261",
        ".h263",
        ".h264",
        ".jpgm",
        ".jpgv",
        ".jpm",
        ".m1v",
        ".m2v",
        ".m4s",
        ".m4u",
        ".m4v",
        ".mj2",
        ".mjp2",
        ".mk3d",
        ".mks",
        ".mkv",
        ".mng",
        ".mov",
        ".movie",
        ".mp4",
        ".mp4v",
        ".mpe",
        ".mpeg",
        ".mpg",
        ".mpg4",
        ".mts",
        ".mxu",
        ".ogv",
        ".pyv",
        ".qt",
        ".smv",
        ".ts",
        ".uvh",
        ".uvm",
        ".uvp",
        ".uvs",
        ".uvu",
        ".uvv",
        ".uvvh",
        ".uvvm",
        ".uvvp",
        ".uvvs",
        ".uvvu",
        ".uvvv",
        ".viv",
        ".vob",
        ".webm",
        ".wm",
        ".wmv",
        ".wmx",
        ".wvx",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> RasterImageExtensions =
        JpegExtensions
            .Concat(WebPImageExtensions)
            .Concat(OtherImageExtensions)
            .Concat(RawImageExtensions)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> ImageExtensions =
        RasterImageExtensions
            .Concat(VectorImageExtensions)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
