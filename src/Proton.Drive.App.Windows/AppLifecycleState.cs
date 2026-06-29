namespace Proton.Drive.App.Windows;

internal sealed record AppLifecycleState(AppWindow CurrentWindow)
{
    public static AppLifecycleState Initial { get; } = new(AppWindow.None);
}
