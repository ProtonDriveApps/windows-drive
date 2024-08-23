using System;
using System.IO;
using ProtonDrive.Shared.Text;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Shared;

public sealed class FileNameFactory<TId> : FileNameFactory, IFileNameFactory<TId>
    where TId : IEquatable<TId>
{
    private readonly string _namePattern;
    private readonly RandomStringGenerator _randomStrings;

    public FileNameFactory(string namePattern)
    {
        _namePattern = namePattern;

        _randomStrings = new RandomStringGenerator(RandomStringCharacterGroup.NumbersAndLatinLowercase);
    }

    public string GetName(IFileSystemNodeModel<TId> nodeModel)
    {
        var originalName = nodeModel.Type == NodeType.File ? Path.GetFileNameWithoutExtension(nodeModel.Name) : nodeModel.Name;
        var extension = nodeModel.Type == NodeType.File ? Path.GetExtension(nodeModel.Name) : string.Empty;

        var name = GenerateName(nodeModel.Id, originalName, extension);
        var cutLength = name.Length - MaxNameLength;
        if (cutLength > originalName.Length)
        {
            originalName = nodeModel.Name;
            extension = string.Empty;
            name = GenerateName(nodeModel.Id, originalName, extension);
        }

        originalName = name.Length > MaxNameLength
            ? originalName.Substring(0, originalName.Length - name.Length + MaxNameLength)
            : originalName;

        return GenerateName(nodeModel.Id, originalName, extension);
    }

    private string GenerateName(TId id, string originalName, string extension)
    {
        var now = DateTime.Now;

        return _namePattern
            .Replace(OriginalNamePlaceholder, originalName)
            .Replace(ExtensionPlaceholder, extension)
            .Replace(CurrentDatePlaceholder, now.ToString("yyyy-MM-dd"))
            .Replace(CurrentTimePlaceholder, now.ToString("HHmmss"))
            .Replace(IdPlaceholder, id.ToString())
            .Replace(RandomSuffixPlaceholder, _randomStrings.GenerateRandomString(RandomSuffixLength));
    }
}
