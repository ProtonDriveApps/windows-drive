using System.Collections.Generic;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Sync;

internal interface ISyncRootPathProvider
{
    IReadOnlyList<string> GetOfTypes(IReadOnlyCollection<MappingType> types);
}
