using System.Data;

namespace ProtonDrive.DataAccess;

public interface IConnectionProvider
{
    IDbConnection Connection { get; }
}
