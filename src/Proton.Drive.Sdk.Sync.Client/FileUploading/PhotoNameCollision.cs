namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

public sealed record PhotoNameCollision(string LinkId, string FileName, string NameHash, string? ContentHash);
