namespace ProtonDrive.Update;

public static class AppUpdateStatusExtensions
{
    public static bool InProgress(this AppUpdateStatus status)
    {
        return status is AppUpdateStatus.Checking or AppUpdateStatus.Downloading;
    }
}
