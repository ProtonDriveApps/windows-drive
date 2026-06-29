namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface IFolderAppearanceCustomizer
{
    public bool TrySetIconAndInfoTip(string folderPath, string iconPath, string infoTip);
}
