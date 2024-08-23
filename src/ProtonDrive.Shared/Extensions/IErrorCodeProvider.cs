using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.Shared.Extensions;

public interface IErrorCodeProvider
{
    bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode);
}
