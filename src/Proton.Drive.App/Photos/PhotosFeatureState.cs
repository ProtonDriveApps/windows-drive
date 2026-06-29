namespace Proton.Drive.App.Photos;

public sealed record PhotosFeatureState(PhotosFeatureStatus Status, string? ErrorMessage = null)
{
    public static PhotosFeatureState Idle { get; } = new(PhotosFeatureStatus.Idle);
}
