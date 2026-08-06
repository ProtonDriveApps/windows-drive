namespace Proton.Drive.Update.Files.Downloadable;

internal interface IDownloadableFile
{
    Task DownloadAsync(string url, string filename);
}
