namespace ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;

public enum ThumbnailGenerationFileSizeRange
{
    LessThan100KiB,
    LessThan1MiB,
    LessThan10MiB,
    LessThan100MiB,
    LessThan1GiB,
    AtLeast1GiB,
}
