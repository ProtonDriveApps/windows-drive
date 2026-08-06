using System.Diagnostics.CodeAnalysis;

namespace Proton.Drive.Shared.Extensions;

public interface IFormattedErrorCodeProvider
{
    bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode);
}
