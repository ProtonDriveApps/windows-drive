using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record FolderCreationResponse : ApiResponse
{
    private FolderId? _folderId;

    [JsonPropertyName("Folder")]
    public FolderId FolderId
    {
        get => _folderId ??= new FolderId();
        init => _folderId = value;
    }
}
