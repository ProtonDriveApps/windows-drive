using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Update.Files.Validatable;

/// <summary>
/// Logs requests and exceptions of <see cref="ValidatableFile"/>.
/// </summary>
internal class LoggingValidatableFile : IValidatableFile
{
    private readonly ILogger<LoggingValidatableFile> _logger;
    private readonly IValidatableFile _origin;

    public LoggingValidatableFile(ILogger<LoggingValidatableFile> logger, IValidatableFile origin)
    {
        _logger = logger;
        _origin = origin;
    }

    public async Task<bool> IsValidAsync(string filename, ReadOnlyMemory<byte> checksum)
    {
        try
        {
            _logger.LogInformation("Validating the app update file \"{FileName}\"", Path.GetFileName(filename));

            var result = await _origin.IsValidAsync(filename, checksum).ConfigureAwait(false);

            if (result)
            {
                _logger.LogInformation("The app update file is valid");
            }
            else
            {
                _logger.LogWarning("The app update file is missing or has an invalid checksum");
            }

            return result;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogError("Failed to validate the app update: {ExceptionType} {HResult}", ex.GetType().Name, ex.HResult);

            throw;
        }
    }
}
