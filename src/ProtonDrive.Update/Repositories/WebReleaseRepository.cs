using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ProtonDrive.Update.Config;
using ProtonDrive.Update.Contracts;
using ProtonDrive.Update.Releases;

namespace ProtonDrive.Update.Repositories;

/// <summary>
/// Reads app release data from provided URL and converts it into sequence of app releases.
/// </summary>
internal class WebReleaseRepository : IReleaseRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly AppUpdateConfig _config;
    private readonly HttpClient _httpClient;

    public WebReleaseRepository(AppUpdateConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<Release>> GetReleasesAsync()
    {
        var releases = await GetReleasesInternalAsync().ConfigureAwait(false);

        return ConvertContract(releases);
    }

    public IEnumerable<Release> GetReleasesFromCache()
    {
        var cacheFilePath = GetCacheFilePath();
        if (!File.Exists(cacheFilePath))
        {
            return [];
        }

        using var jsonStream = File.Open(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

        var releases = JsonSerializer.Deserialize<ReleasesContract>(jsonStream, JsonOptions);

        return releases is not null ? ConvertContract(releases) : [];
    }

    public void ClearReleasesCache()
    {
        var cacheFilePath = GetCacheFilePath();

        if (File.Exists(cacheFilePath))
        {
            File.Delete(cacheFilePath);
        }
    }

    private async Task<ReleasesContract> GetReleasesInternalAsync()
    {
        using var response = await _httpClient.GetAsync(_config.FeedUri).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("Response status code is not success");
        }

        await SaveContentToCacheAsync(response.Content).ConfigureAwait(false);

        // ASP.NET by default uses case-insensitive property name matching during
        // JSON deserialization. This conflicts with FileContract having two properties with
        // names different only in case. Overriding default ASP.NET deserialization options is necessary.
        var result = await response.Content.ReadFromJsonAsync<ReleasesContract>(JsonOptions).ConfigureAwait(false);

        return result ?? throw new JsonException();
    }

    private IEnumerable<Release> ConvertContract(ReleasesContract releases)
    {
        if (!releases.Releases.Any())
        {
            return new CategoryReleases(releases.Categories, _config.CurrentVersion, _config.EarlyAccessCategoryName);
        }

        return releases.Releases
            .Select(
                r => new Release(
                    r,
                    string.Equals(_config.EarlyAccessCategoryName, r.CategoryName, StringComparison.OrdinalIgnoreCase),
                    _config.CurrentVersion));
    }

    private async Task SaveContentToCacheAsync(HttpContent content)
    {
        var cacheFileStream = File.Open(GetCacheFilePath(), FileMode.Create, FileAccess.Write, FileShare.None);
        await using (cacheFileStream.ConfigureAwait(false))
        {
            await content.CopyToAsync(cacheFileStream).ConfigureAwait(false);
        }
    }

    private string GetCacheFilePath()
    {
        Directory.CreateDirectory(_config.UpdatesFolderPath);
        return Path.Combine(_config.UpdatesFolderPath, "version.json");
    }
}
