using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Shared.Text;

namespace ProtonDrive.Client.BugReport;

internal class BugReportClient : IBugReportClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RandomStringGenerator _randomStringGenerator;

    public BugReportClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _randomStringGenerator = new RandomStringGenerator(RandomStringCharacterGroup.NumberAndLatin);
    }

    public async Task SendAsync(BugReportBody body, Stream? attachment, CancellationToken cancellationToken)
    {
        var reportClient = _httpClientFactory.CreateClient(ApiClientConfigurator.CoreHttpClientName);

        using var report = GetReport(body);

        if (attachment is not null)
        {
            var logs = new StreamContent(attachment);

            report.Add(logs, "App-Logs", "Drive-Logs.zip");
        }

        await reportClient.PostAsync("v4/reports/bug", report, cancellationToken)
            .ReadFromJsonAsync<ApiResponse>(cancellationToken).ThrowOnFailure().ConfigureAwait(false);
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
