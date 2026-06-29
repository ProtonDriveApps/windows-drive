namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public sealed record MappingErrorInfo
{
    public MappingErrorInfo(MappingErrorCode errorCode, string? conflictingProviderName = null)
    {
        ErrorCode = errorCode;
        ConflictingProviderName = conflictingProviderName;
    }

    public MappingErrorCode ErrorCode { get; init; }

    public string? ConflictingProviderName { get; init; }
}
