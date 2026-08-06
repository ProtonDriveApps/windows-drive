using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class FileConsistencyGuardException : Exception, IFormattedErrorCodeProvider
{
    public FileConsistencyGuardException()
    {
    }

    public FileConsistencyGuardException(string message)
        : base(message)
    {
    }

    public FileConsistencyGuardException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public FileConsistencyGuardException(FileConsistencyGuardErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    public FileConsistencyGuardErrorCode? ErrorCode { get; }

    public bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        if (ErrorCode is null)
        {
            formattedErrorCode = null;
            return false;
        }

        formattedErrorCode = ErrorCode.Value.ToString();
        return true;
    }
}
