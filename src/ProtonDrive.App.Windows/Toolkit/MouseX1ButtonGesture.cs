using System.Windows.Input;

namespace ProtonDrive.App.Windows.Toolkit;

internal sealed class MouseX1ButtonGesture : MouseGesture
{
    public static MouseX1ButtonGesture Instance { get; } = new();

    public override bool Matches(object targetElement, InputEventArgs inputEventArgs)
    {
        if (inputEventArgs is not MouseButtonEventArgs mouseEventArgs)
        {
            return false;
        }

        return mouseEventArgs is { ChangedButton: MouseButton.XButton1 };
    }
}
