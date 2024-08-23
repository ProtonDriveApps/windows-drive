namespace ProtonDrive.App.SystemIntegration;

internal interface IFileProtector
{
    public bool FileMustBeProtected(string rootPath);
    public void ProtectFile(string filePath);
}
