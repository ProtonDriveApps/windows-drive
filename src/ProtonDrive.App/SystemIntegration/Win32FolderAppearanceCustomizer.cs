using System;
using System.IO;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.SystemIntegration;

public class Win32FolderAppearanceCustomizer : IFolderAppearanceCustomizer
{
    private const string IniFileName = "desktop.ini";

    public bool TrySetIconAndInfoTip(string folderPath, string iconPath, string infoTip)
    {
        if (!Directory.Exists(folderPath))
        {
            return false;
        }

        try
        {
            SetIconAndInfoTip(folderPath, iconPath, infoTip);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            return false;
        }
    }

    public void SetIconAndInfoTip(string folderPath, string iconPath, string infoTip)
    {
        var iniFilePath = Path.Combine(folderPath, IniFileName);

        using var streamWriter = new StreamWriter(iniFilePath, append: true);

        if (streamWriter.BaseStream.Length > 0)
        {
            return;
        }

        streamWriter.Write(GetIniFileContents(iconPath, infoTip));

        SetFileAttribute(folderPath, FileAttributes.ReadOnly);
        SetFileAttribute(iniFilePath, FileAttributes.System);
        SetFileAttribute(iniFilePath, FileAttributes.Hidden);
    }

    private static void SetFileAttribute(string path, FileAttributes fileAttribute)
    {
        if ((File.GetAttributes(path) & fileAttribute) != fileAttribute)
        {
            File.SetAttributes(path, File.GetAttributes(path) | fileAttribute);
        }
    }

    private static string GetIniFileContents(string iconPath, string infoTip)
    {
        return @$"[.ShellClassInfo]
IconFile={iconPath}
IconIndex=0
InfoTip={infoTip}";
    }
}
