namespace ProtonDrive.App.SystemIntegration;

public interface IFolderAppearanceCustomizer
{
    public bool TrySetIconAndInfoTip(string folderPath, string iconPath, string infoTip);
}
