namespace ProtonDrive.App.Drive.Services.Shared;

public sealed record DataServiceState
{
    public DataServiceStatus Status { get; init; }

    public int NumberOfFailedItems { get; set; }

    public static DataServiceState Initial { get; } = new();
}
