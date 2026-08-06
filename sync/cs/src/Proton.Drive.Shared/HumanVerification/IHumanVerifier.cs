namespace Proton.Drive.Shared.HumanVerification;

public interface IHumanVerifier
{
    Task<string?> VerifyAsync(string captchaToken, CancellationToken cancellationToken);
}
