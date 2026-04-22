namespace ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;

public enum ThumbnailGenerationDurationRange
{
    LessThan400Ms,
    LessThan1Sec,
    LessThan3Sec,
    LessThan10Sec,
    LessThan30Sec,
    AtLeast30Sec,
}
