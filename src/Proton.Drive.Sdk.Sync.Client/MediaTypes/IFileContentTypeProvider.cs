namespace Proton.Drive.Sdk.Sync.Client.MediaTypes;

public interface IFileContentTypeProvider
{
    string GetContentType(string filename);
}
