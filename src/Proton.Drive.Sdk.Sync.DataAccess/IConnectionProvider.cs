using System.Data;

namespace Proton.Drive.Sdk.Sync.DataAccess;

public interface IConnectionProvider
{
    IDbConnection Connection { get; }
}
