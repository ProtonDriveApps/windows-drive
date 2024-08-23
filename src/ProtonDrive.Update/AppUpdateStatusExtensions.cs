namespace ProtonDrive.Update;

public static class AppUpdateStatusExtensions
{
    public static bool InProgress(this AppUpdateStatus status)
    {
        return status == AppUpdateStatus.Checking ||
               status == AppUpdateStatus.Downloading;
    }
}
