namespace Proton.Drive.Sdk.Sync.Shared.Diagnostics.Metrics.Thumbnails;

public enum ThumbnailGenerationFileSizeRange
{
    NotAvailable,
    LessThan100KiB,
    LessThan1MiB,
    LessThan10MiB,
    LessThan100MiB,
    LessThan1GiB,
    AtLeast1GiB,
}
