namespace Proton.Drive.App.Windows.Dialogs.HumanVerification;

internal sealed record CaptchaMessage(string Type, string Token, int Height);
