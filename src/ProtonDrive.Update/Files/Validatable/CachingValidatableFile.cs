using System;
using System.IO;
using System.Threading.Tasks;

namespace ProtonDrive.Update.Files.Validatable;

/// <summary>
/// Caches positive file validation result while file length and modification date has not changed.
/// </summary>
internal class CachingValidatableFile : IValidatableFile
{
    private readonly IValidatableFile _origin;

    private string? _filename;
    private ReadOnlyMemory<byte>? _checksum;
    private long _fileLength;
    private DateTime _modifiedAt;

    public CachingValidatableFile(IValidatableFile origin)
    {
        _origin = origin;
    }

    public async Task<bool> IsValidAsync(string filename, ReadOnlyMemory<byte> checksum)
    {
        if (CacheContains(filename, checksum) && !FileChanged(filename))
        {
            return true;
        }

        ClearCache();

        var valid = await _origin.IsValidAsync(filename, checksum).ConfigureAwait(false);

        if (valid)
        {
            AddToCache(filename, checksum);
        }

        return valid;
    }

    private bool CacheContains(string filename, ReadOnlyMemory<byte> checksum)
    {
        return _filename == filename && _checksum.GetValueOrDefault().Span.SequenceEqual(checksum.Span);
    }

    private bool FileChanged(string filename)
    {
        var fileInfo = new FileInfo(filename);

        return !fileInfo.Exists ||
               fileInfo.LastWriteTimeUtc != _modifiedAt ||
               fileInfo.Length != _fileLength;
    }

    private void ClearCache()
    {
        _filename = null;
        _checksum = null;
    }

    private void AddToCache(string filename, ReadOnlyMemory<byte> checksum)
    {
        var fileInfo = new FileInfo(filename);

        _modifiedAt = fileInfo.LastWriteTimeUtc;
        _fileLength = fileInfo.Length;

        _filename = filename;
        _checksum = checksum;
    }
}
