using Microsoft.Extensions.Logging.Abstractions;
using Proton.Drive.Sdk.Sync.Shared.Ignore;

namespace Proton.Drive.Sdk.Sync.Shared.Tests.Ignore;

/// <summary>
/// An ignore file in the on-demand folder can be a cloud files placeholder whose content is not on
/// disk. Reading it makes Windows ask the sync provider for the content, and the sync provider is the
/// very process doing the reading, so update detection blocks until the cloud files timeout expires.
/// </summary>
/// <remarks>
/// A real placeholder cannot be created without a registered sync root. A cloud-only placeholder
/// carries both <see cref="FileAttributes.Offline"/> and FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS, and of
/// the two only <see cref="FileAttributes.Offline"/> can be set on an ordinary file, so these tests use
/// it to stand in for a placeholder. Such a file is still readable, which is what lets a test notice
/// whether the provider read it.
/// </remarks>
public sealed class FileSystemIgnoreRuleProviderTests : IDisposable
{
    private const int RootId = 1;

    private readonly string _rootPath;

    public FileSystemIgnoreRuleProviderTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "ProtonIgnoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    public void Dispose()
    {
        foreach (var filePath in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public void IsIgnored_AppliesIgnoreFileWhoseContentIsOnDisk()
    {
        WriteIgnoreFile("repo", "*.log");
        var provider = CreateProvider();

        var isIgnored = provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void IsIgnored_DoesNotReadIgnoreFileWhoseContentIsNotOnDisk()
    {
        var filePath = WriteIgnoreFile("repo", "*.log");
        MarkContentNotOnDisk(filePath);
        var provider = CreateProvider();

        var isIgnored = provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        isIgnored.Should().BeFalse("reading a placeholder from the sync provider's own process blocks until the cloud files timeout");
    }

    [Fact]
    public void IsIgnored_AppliesIgnoreFileOnceItsContentArrivesOnDisk()
    {
        var filePath = WriteIgnoreFile("repo", "*.log");
        MarkContentNotOnDisk(filePath);
        var provider = CreateProvider();
        provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        MarkContentOnDisk(filePath);
        var isIgnored = provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        isIgnored.Should().BeTrue("hydrating a file changes neither its size nor its last write time, yet its rules have to be read now");
    }

    [Fact]
    public void IsIgnored_KeepsRulesReadBeforeTheContentLeftTheDisk()
    {
        var filePath = WriteIgnoreFile("repo", "*.log");
        var provider = CreateProvider();
        provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        MarkContentNotOnDisk(filePath);
        var isIgnored = provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        isIgnored.Should().BeTrue("dehydrating a file with unchanged size and last write time leaves its content as it was read");
    }

    [Fact]
    public void IsIgnored_DoesNotKeepRulesOnceAPlaceholderWithOtherContentReplacesTheFile()
    {
        var filePath = WriteIgnoreFile("repo", "*.log");
        var provider = CreateProvider();
        provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        WriteIgnoreFile("repo", "*.tmp");
        File.SetLastWriteTimeUtc(filePath, File.GetLastWriteTimeUtc(filePath).AddMinutes(1));
        MarkContentNotOnDisk(filePath);
        var isIgnored = provider.IsIgnored(RootId, _rootPath, @"repo\build.log", isDirectory: false);

        isIgnored.Should().BeFalse("the rules of the previous version must not outlive it");
    }

    [Theory]
    [InlineData((FileAttributes)0x00401620)] // A cloud-only placeholder as seen in a sync root
    [InlineData((FileAttributes)0x00400000)] // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
    [InlineData((FileAttributes)0x00040000)] // FILE_ATTRIBUTE_RECALL_ON_OPEN
    [InlineData(FileAttributes.Offline)]
    public void IsContentOnDisk_IsFalseForPlaceholderAttributes(FileAttributes attributes)
    {
        FileSystemIgnoreRuleProvider.IsContentOnDisk(attributes).Should().BeFalse();
    }

    [Theory]
    [InlineData(FileAttributes.Normal)]
    [InlineData(FileAttributes.Archive)]
    [InlineData(FileAttributes.Archive | FileAttributes.ReparsePoint | FileAttributes.SparseFile | (FileAttributes)0x00080000)] // A hydrated, pinned placeholder
    [InlineData(FileAttributes.ReadOnly | FileAttributes.Hidden)]
    public void IsContentOnDisk_IsTrueForFilesPresentLocally(FileAttributes attributes)
    {
        FileSystemIgnoreRuleProvider.IsContentOnDisk(attributes).Should().BeTrue();
    }

    private static void MarkContentNotOnDisk(string filePath)
    {
        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Offline);
    }

    private static void MarkContentOnDisk(string filePath)
    {
        File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.Offline);
    }

    private static FileSystemIgnoreRuleProvider CreateProvider()
    {
        // Revalidate on every call, so that attribute changes made by a test are seen at once
        return new FileSystemIgnoreRuleProvider(
            NullLogger<FileSystemIgnoreRuleProvider>.Instance,
            honoursGitIgnoreFiles: true,
            revalidationInterval: TimeSpan.Zero);
    }

    private string WriteIgnoreFile(string relativeDirectoryPath, string content)
    {
        var directoryPath = Path.Combine(_rootPath, relativeDirectoryPath);
        Directory.CreateDirectory(directoryPath);

        var filePath = Path.Combine(directoryPath, FileSystemIgnoreRuleProvider.GitIgnoreFileName);

        if (File.Exists(filePath))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        File.WriteAllText(filePath, content + Environment.NewLine);

        return filePath;
    }
}
