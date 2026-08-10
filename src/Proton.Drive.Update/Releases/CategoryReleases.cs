using System.Collections;
using Proton.Drive.Update.Contracts;

namespace Proton.Drive.Update.Releases;

/// <summary>
/// Transforms deserialized release data (stream of <see cref="CategoryContract"/>) into stream of <see cref="Release"/>.
/// </summary>
internal class CategoryReleases : IEnumerable<Release>
{
    private readonly IEnumerable<CategoryContract> _categories;
    private readonly Version _currentVersion;
    private readonly string _earlyAccessCategoryName;

    public CategoryReleases(IEnumerable<CategoryContract> categories, Version currentVersion, string earlyAccessCategoryName)
    {
        _categories = categories;
        _currentVersion = currentVersion;
        _earlyAccessCategoryName = earlyAccessCategoryName;
    }

    public IEnumerator<Release> GetEnumerator()
    {
        foreach (var category in _categories)
        {
            var earlyAccess = string.Equals(_earlyAccessCategoryName, category.Name, StringComparison.OrdinalIgnoreCase);

            foreach (var release in category.Releases)
            {
                yield return new Release(release, earlyAccess, _currentVersion);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
