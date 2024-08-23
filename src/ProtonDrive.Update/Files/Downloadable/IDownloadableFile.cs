using System.Threading.Tasks;

namespace ProtonDrive.Update.Files.Downloadable;

internal interface IDownloadableFile
{
    Task DownloadAsync(string url, string filename);
}
