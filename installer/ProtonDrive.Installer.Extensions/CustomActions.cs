using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace ProtonDrive.Installer.Extensions;

public class CustomActions
{
    private static readonly Guid UserProgramFilesFolderId = new("5cd7aee2-2219-4a67-b85d-6c9ce15660cb");
    private static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();

    [Flags]
    private enum KnownFolderFlags : uint
    {
        Create = 0x8000,
    }

    [CustomAction]
    public static ActionResult DoPerMachineUpgradeSupportActions(Session session)
    {
        var upgradeHadAlreadyBeenDetected = UpgradeHasBeenDetected();

        var allUsersInitialValue = session["ALLUSERS"];
        var msiInstallPerUserInitialValue = session["MSIINSTALLPERUSER"];

        session["ALLUSERS"] = "1";
        session["MSIINSTALLPERUSER"] = string.Empty;

        session.DoAction("FindRelatedProducts");

        var isUpgradeFromPerMachineInstallation = !upgradeHadAlreadyBeenDetected && UpgradeHasBeenDetected();

        if (isUpgradeFromPerMachineInstallation)
        {
            session["DesktopFolderToSearchForShortcut"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        }

        session["ALLUSERS"] = allUsersInitialValue;
        session["MSIINSTALLPERUSER"] = msiInstallPerUserInitialValue;

        return ActionResult.Success;

        bool UpgradeHasBeenDetected() => !string.IsNullOrEmpty(session["WIX_UPGRADE_DETECTED"]);
    }

    [CustomAction]
    public static ActionResult QueryUserProgramFilesFolder(Session session)
    {
        var hr = SHGetKnownFolderPath(in UserProgramFilesFolderId, KnownFolderFlags.Create, IntPtr.Zero, out var pathPointer);

        try
        {
            if (hr != 0)
            {
                session.Log($"Failed to get user program files folder path. (HRESULT = 0x{hr:X8})");
                return ActionResult.Failure;
            }

            var path = Marshal.PtrToStringUni(pathPointer);
            if (string.IsNullOrEmpty(path))
            {
                session.Log("Failed to marshal user program files folder path string.");
                return ActionResult.Failure;
            }

            session["USERPROGRAMFILESFOLDER"] = path.EndsWith(DirectorySeparatorString) ? path : path + DirectorySeparatorString;

            return ActionResult.Success;
        }
        finally
        {
            if (pathPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
    }

    [CustomAction]
    public static ActionResult HideCancelButton(Session session)
    {
        using var record = new Record(2);
        record.SetInteger(1, 2);
        record.SetInteger(2, 0);
        session.Message(InstallMessage.CommonData, record);

        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult RemoveStartupEntry(Session session)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

            runKey?.DeleteValue(session["ProductName"]);

            return ActionResult.Success;
        }
        catch (Exception ex)
        {
            session.Log("Ignoring error while removing startup entry: {0}", ex);
            return ActionResult.NotExecuted;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, KnownFolderFlags dwFlags, IntPtr hToken, out IntPtr pszPath);
}
