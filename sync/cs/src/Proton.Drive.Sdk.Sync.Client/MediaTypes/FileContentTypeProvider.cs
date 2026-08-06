using System.Net.Mime;
using Microsoft.AspNetCore.StaticFiles;

namespace Proton.Drive.Sdk.Sync.Client.MediaTypes;

internal sealed class FileContentTypeProvider : IFileContentTypeProvider
{
    private static readonly Dictionary<string, string> FileExtensionToMimeTypeMappings = new()
    {
        { ".apk", "application/vnd.android.package-archive" },
        { ".apng", "image/apng" },
        { ".arc", "application/x-freearc" },
        { ".avif", "image/avif" },
        { ".bzip2", "application/x-bzip2" },
        { ".cr3", "image/x-canon-cr3" },
        { ".epub", "application/epub+zip" },
        { ".flac", "audio/flac" },
        { ".gzip", "application/gzip" },
        { ".heic", "image/heic" },
        { ".heics", "image/heic-sequence" },
        { ".heif", "image/heif" },
        { ".heifs", "image/heif-sequence" },
        { ".keynote", "application/vnd.apple.keynote" },
        { ".mp1s", "video/mp1s" },
        { ".mp2p", "video/mp2p" },
        { ".mp2t", "video/mp2t" },
        { ".mp4a", "audio/mp4" },
        { ".numbers", "application/vnd.apple.numbers" },
        { ".odb", "application/vnd.oasis.opendocument.base" },
        { ".odc", "application/vnd.oasis.opendocument.chart" },
        { ".odf", "application/vnd.oasis.opendocument.formula" },
        { ".odg", "application/vnd.oasis.opendocument.graphics" },
        { ".odi", "application/vnd.oasis.opendocument.image" },
        { ".odm", "application/vnd.oasis.opendocument.text-master" },
        { ".odp", "application/vnd.oasis.opendocument.presentation" },
        { ".ods", "application/vnd.oasis.opendocument.spreadsheet" },
        { ".odt", "application/vnd.oasis.opendocument.text" },
        { ".opus", "audio/opus" },
        { ".otc", "application/vnd.oasis.opendocument.chart-template" },
        { ".otf", "application/vnd.oasis.opendocument.formula-template" },
        { ".otg", "application/vnd.oasis.opendocument.graphics-template" },
        { ".oth", "application/vnd.oasis.opendocument.text-web" },
        { ".oti", "application/vnd.oasis.opendocument.image-template" },
        { ".otp", "application/vnd.oasis.opendocument.presentation-template" },
        { ".ots", "application/vnd.oasis.opendocument.spreadsheet-template" },
        { ".ott", "application/vnd.oasis.opendocument.text-template" },
        { ".pages", "application/vnd.apple.pages" },
        { ".qcp", "audio/qcelp" },
        { ".v3g2", "video/3gpp2" },
        { ".v3gp", "video/3gpp" },
        { ".x7zip", "application/x-7z-compressed" },
    };

    private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = CreateProvider();

    public string GetContentType(string filename)
    {
        return _fileExtensionContentTypeProvider.TryGetContentType(filename, out var contentType)
            ? contentType
            : MediaTypeNames.Application.Octet;
    }

    private static FileExtensionContentTypeProvider CreateProvider()
    {
        // Provider contains a set of default file extension to MIME type mappings
        var provider = new FileExtensionContentTypeProvider();

        // We add missing mappings and update existing ones
        foreach (var mapping in FileExtensionToMimeTypeMappings)
        {
            provider.Mappings[mapping.Key] = mapping.Value;
        }

        return provider;
    }
}
