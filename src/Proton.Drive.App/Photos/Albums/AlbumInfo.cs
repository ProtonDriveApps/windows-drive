namespace Proton.Drive.App.Photos.Albums;

public sealed record AlbumInfo(
    string LinkId,
    string Name,
    int PhotoCount,
    DateTime LastActivityTime,
    bool BiometricsRequired);
