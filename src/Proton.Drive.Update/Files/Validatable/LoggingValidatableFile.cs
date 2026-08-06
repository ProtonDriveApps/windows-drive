using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Update.Files.Validatable;

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

    public async Task<bool> IsValidAsync(string filePath, ReadOnlyMemory<byte> checksum)
    {
        var fileName = Path.GetFileName(filePath);

        try
        {
            _logger.LogInformation("Validating the app update file \"{FileName}\"", fileName);

            var result = await _origin.IsValidAsync(filePath, checksum).ConfigureAwait(false);

            if (result)
            {
                _logger.LogInformation("The app update file \"{FileName}\" is valid", fileName);
            }
            else
            {
                _logger.LogWarning("The app update file \"{FileName}\" is missing or has an invalid checksum", fileName);
            }

            return result;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogError("Failed to validate the app update file \"{FileName}\": {ExceptionType} {HResult}", fileName, ex.GetType().Name, ex.HResult);

            throw;
        }
    }
}
