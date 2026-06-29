using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

public sealed class LocalStorageOptimizationException : Exception, IFormattedErrorCodeProvider
{
    public LocalStorageOptimizationException()
    {
    }

    public LocalStorageOptimizationException(string message)
        : base(message)
    {
    }

    public LocalStorageOptimizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public LocalStorageOptimizationException(RemoteToLocalMapping mapping)
    {
        MappingType = mapping.Type;
        IsStorageOptimizationEnabled = mapping.Local.StorageOptimization?.IsEnabled ?? false;
        ErrorCode = mapping.Local.StorageOptimization?.ErrorCode;
        ConflictingProviderName = mapping.Local.StorageOptimization?.ConflictingProviderName;
    }

    private MappingType? MappingType { get; }
    private bool IsStorageOptimizationEnabled { get; }
    private StorageOptimizationErrorCode? ErrorCode { get; }
    private string? ConflictingProviderName { get; }

    public bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        formattedErrorCode = string.Empty;

        if (MappingType is null || ErrorCode is null)
        {
            return false;
        }

        // Upon failure, the storage optimization enabled status is already reverted
        var enabled = IsStorageOptimizationEnabled ? "Disabling" : "Enabling";

        formattedErrorCode = $"{MappingType}:{enabled}/{ErrorCode}";

        if (!string.IsNullOrEmpty(ConflictingProviderName))
        {
            formattedErrorCode += $":\"{ConflictingProviderName}\"";
        }

        return true;
    }
}
