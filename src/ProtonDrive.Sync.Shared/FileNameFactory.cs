namespace ProtonDrive.Sync.Shared;

public abstract class FileNameFactory
{
    public const string OriginalNamePlaceholder = "{OriginalName}";
    public const string ExtensionPlaceholder = "{.Extension}";
    public const string CurrentDatePlaceholder = "{CurrentDate}";
    public const string CurrentTimePlaceholder = "{CurrentTime}";
    public const string IdPlaceholder = "{ID}";
    public const string RandomSuffixPlaceholder = "{RandomSuffix}";

    public const int RandomSuffixLength = 6;
    public const int MaxNameLength = 255;
}
