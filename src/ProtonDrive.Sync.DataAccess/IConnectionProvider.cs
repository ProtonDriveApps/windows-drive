using System.Data;

namespace ProtonDrive.Sync.DataAccess;

public interface IConnectionProvider
{
    IDbConnection Connection { get; }
}
