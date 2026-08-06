using System.Diagnostics.CodeAnalysis;
using System.Net;
using Proton.Drive.Shared.Net.Http;

namespace Proton.Drive.Shared.HumanVerification;

public class HumanVerificationHandler(IHumanVerifier humanVerifier) : DelegatingHandler
{
    public const int HumanVerificationRequiredCode = 9001;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is not HttpStatusCode.UnprocessableEntity)
        {
            return response;
        }

        var apiResponse = await response.TryReadFromJsonAsync<ApiResponse>(cancellationToken).ConfigureAwait(false);

        if (!IsVerificationRequired(apiResponse, out var captchaToken))
        {
            return response;
        }

        var result = await HandleHumanVerificationAsync(request, captchaToken, cancellationToken).ConfigureAwait(false);

        return result ?? response;
    }

    private static bool IsVerificationRequired(ApiResponse? response, [MaybeNullWhen(false)] out string humanVerificationToken)
    {
        if (response?.Code != HumanVerificationRequiredCode
           || response.Details == null
           || string.IsNullOrEmpty(response.Details.HumanVerificationToken)
           || !response.Details.HumanVerificationMethods.Contains("captcha"))
        {
            humanVerificationToken = null;
            return false;
        }

        humanVerificationToken = response.Details.HumanVerificationToken;
        return true;
    }

    private async Task<HttpResponseMessage?> HandleHumanVerificationAsync(HttpRequestMessage request, string captchaToken, CancellationToken cancellationToken)
    {
        var verificationToken = await humanVerifier.VerifyAsync(captchaToken, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(verificationToken))
        {
            return null;
        }

        request.Headers.Add("x-pm-human-verification-token-type", "captcha");
        request.Headers.Add("x-pm-human-verification-token", verificationToken);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    internal sealed record ApiResponse(int Code, ApiResponseDetails? Details);

    // ReSharper disable once ClassNeverInstantiated.Local
    internal sealed record ApiResponseDetails(string? HumanVerificationToken, IReadOnlyList<string> HumanVerificationMethods);
}
