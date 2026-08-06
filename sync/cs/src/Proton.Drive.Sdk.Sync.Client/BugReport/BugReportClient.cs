using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Text;

namespace Proton.Drive.Sdk.Sync.Client.BugReport;

internal class BugReportClient : IBugReportClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RandomStringGenerator _randomStringGenerator;

    public BugReportClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _randomStringGenerator = new RandomStringGenerator(RandomStringCharacterGroup.NumberAndLatin);
    }

    public async Task SendAsync(BugReportBody body, IReadOnlyCollection<BugReportAttachment> attachments, CancellationToken cancellationToken)
    {
        var reportClient = _httpClientFactory.CreateClient(ApiClientConfigurator.CoreHttpClientName);

        using var report = GetReport(body);

        if (attachments.Count > 0)
        {
            foreach (var attachment in attachments)
            {
                var content = new StreamContent(attachment.Stream);

                report.Add(content, attachment.Name, attachment.FileName);
            }
        }

        await reportClient.PostAsync("v4/reports/bug", report, cancellationToken)
            .ReadFromJsonAsync<ApiResponse>(cancellationToken)
            .ThrowOnFailure()
            .ConfigureAwait(false);
    }

    private MultipartFormDataContent GetReport(BugReportBody parameters)
    {
        var boundary = "----WebKitFormBoundary" + _randomStringGenerator.GenerateRandomString(15);

        var content = new MultipartFormDataContent(boundary: boundary);

        foreach (var parameter in parameters.AsDictionary())
        {
            content.Add(new StringContent(parameter.Value), parameter.Key);
        }

        return content;
    }
}
