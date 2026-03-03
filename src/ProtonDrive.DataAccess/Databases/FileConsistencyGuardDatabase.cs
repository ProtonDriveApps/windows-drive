using System.Data;
using Dapper;
using ProtonDrive.DataAccess.Databases.Migrations;
using ProtonDrive.DataAccess.Repositories;

namespace ProtonDrive.DataAccess.Databases;

public sealed class FileConsistencyGuardDatabase : Database
{
    public FileConsistencyGuardDatabase(DatabaseConfig config)
        : base(config)
    {
        FileRepository = new FileConsistencyGuardRepository(this);
    }

    public FileConsistencyGuardRepository FileRepository { get; }

    protected override void SetupDatabase(IDbConnection connection)
    {
        base.SetupDatabase(connection);

        connection.Execute("CREATE TABLE IF NOT EXISTS Files(" +
            "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
            "LocalRootId INTEGER NOT NULL, " +
            "RemoteRootId INTEGER NOT NULL, " +
            "LocalId INTEGER NOT NULL, " +
            "RemoteId TEXT NOT NULL, " +
            "Name TEXT NOT NULL, " +
            "LocalSize INTEGER NOT NULL, " +
            "RemoteSize INTEGER NOT NULL, " +
            "RemotePlainSize INTEGER, " +
            "RemoteSizeOnStorage INTEGER, " +
            "LocalLastWriteTime TEXT NOT NULL, " +
            "RemoteLastWriteTime TEXT NOT NULL, " +
            "RevisionId TEXT, " +
            "ContentVersion INTEGER NOT NULL, " +
            "LocalHash TEXT, " +
            "RemoteHash TEXT, " +
            "TrailingZeroBytesLength INTEGER, " +
            "Status INTEGER NOT NULL, " +
            "Reason INTEGER NOT NULL, " +
            "Error INTEGER NOT NULL" +
            ")");

        connection.Execute("CREATE INDEX IF NOT EXISTS Files_Idx_Status_Reason_Error ON Files(Status, Reason, Error)");

        if (!ColumnExists(connection, "Files", "RemotePlainSize"))
        {
            connection.Execute("ALTER TABLE Files ADD COLUMN RemotePlainSize INTEGER");
        }

        if (!ColumnExists(connection, "Files", "RemoteSizeOnStorage"))
        {
            connection.Execute("ALTER TABLE Files ADD COLUMN RemoteSizeOnStorage INTEGER");
        }

        if (!ColumnExists(connection, "Files", "TrailingZeroBytesLength"))
        {
            connection.Execute("ALTER TABLE Files ADD COLUMN TrailingZeroBytesLength INTEGER");
        }

        if (ColumnExists(connection, "Files", "LastByteIsNonZero"))
        {
            connection.Execute("ALTER TABLE Files DROP COLUMN LastByteIsNonZero");
        }

        new FileConsistencyGuardDataMigration(connection).Execute();
    }
}
