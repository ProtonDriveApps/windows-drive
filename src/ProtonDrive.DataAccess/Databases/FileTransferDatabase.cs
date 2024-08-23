using System.Data;
using Dapper;
using ProtonDrive.DataAccess.Repositories;
using ProtonDrive.Sync.Adapter;

namespace ProtonDrive.DataAccess.Databases;

public sealed class FileTransferDatabase : Database
{
    public FileTransferDatabase(DatabaseConfig config)
        : base(config)
    {
        RevisionUploadAttemptRepository = new RevisionUploadAttemptRepository(this);
    }

    public IRevisionUploadAttemptRepository RevisionUploadAttemptRepository { get; }

    protected override void SetupDatabase(IDbConnection connection)
    {
        base.SetupDatabase(connection);

        connection.Execute("CREATE TABLE IF NOT EXISTS RevisionUploadAttempt (" +
                           "ParentLinkId TEXT NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "LinkId TEXT NOT NULL, " +
                           "RevisionId TEXT NULL, " +
                           "PRIMARY KEY(ParentLinkId, Name))");
    }
}
