namespace Proton.Drive.App.SystemIntegration;

public interface IKnownFolders
{
    Guid Documents { get; }
    Guid Pictures { get; }
    Guid Videos { get; }
    Guid Music { get; }
    Guid Downloads { get; }
    Guid Desktop { get; }

    ILookup<string, Guid> IdsByPath { get; }

    string? GetPath(Guid knownFolderGuid);
}
