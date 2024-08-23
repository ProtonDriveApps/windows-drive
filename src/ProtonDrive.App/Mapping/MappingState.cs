namespace ProtonDrive.App.Mapping;

public record MappingState(MappingSetupStatus Status)
{
    public MappingErrorCode ErrorCode { get; init; }

    public static MappingState None { get; } = new(MappingSetupStatus.None);
    public static MappingState Success { get; } = new(MappingSetupStatus.Succeeded);

    public static MappingState Failure(MappingErrorCode errorCode)
    {
        return new MappingState(MappingSetupStatus.Failed)
        {
            ErrorCode = errorCode,
        };
    }
}
