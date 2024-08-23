using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ProtonDrive.Update.Files.Downloadable;

/// <summary>
/// Downloads file from internet.
/// </summary>
internal class DownloadableFile : IDownloadableFile
{
    private const int FileBufferSize = 16_768;

    private readonly HttpClient _client;

    public DownloadableFile(HttpClient client)
    {
        _client = client;
    }

    public async Task DownloadAsync(string url, string filename)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using (contentStream.ConfigureAwait(false))
        {
            var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, FileBufferSize, true);
            await using (fileStream.ConfigureAwait(false))
            {
                await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }
    }
}
