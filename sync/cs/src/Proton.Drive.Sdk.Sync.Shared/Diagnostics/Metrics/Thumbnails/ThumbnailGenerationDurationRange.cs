namespace Proton.Drive.Sdk.Sync.Shared.Diagnostics.Metrics.Thumbnails;

public enum ThumbnailGenerationDurationRange
{
    LessThan400Ms,
    LessThan1Sec,
    LessThan3Sec,
    LessThan10Sec,
    LessThan30Sec,
    AtLeast30Sec,
}
