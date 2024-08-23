using System.Data;
using System.Linq;
using Dapper;
using ProtonDrive.DataAccess.Repositories;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.DataAccess.Databases;

public sealed class LocalAdapterDatabase : AdapterDatabase
{
    public LocalAdapterDatabase(DatabaseConfig config)
        : base(config, isSyncStateMaintenanceSupported: true)
    {
        AdapterTreeRepository = new LooseCompoundAltIdentifiableTreeNodeRepository<AdapterTreeNodeModel<long, long>, FlattenedAdapterTreeNodeModel<long, long>, long>(
            this,
            "AdapterTree",
            FileSystemNodeExtraColumns
                .Union(AdapterNodeExtraColumns),
            new FlatteningAdapterTreeNodeModelConverter<long, long>());
    }

    public ILooseCompoundAltIdentifiableTreeNodeRepository<AdapterTreeNodeModel<long, long>, long, long> AdapterTreeRepository { get; }

    protected override void SetupDatabase(IDbConnection connection)
    {
        base.SetupDatabase(connection);

        // AdapterTree
        connection.Execute("CREATE TABLE IF NOT EXISTS AdapterTree(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "ParentId INTEGER NOT NULL REFERENCES AdapterTree(Id), " +
                           "VolumeId INTEGER, " +
                           "AltId INTEGER, " +
                           "Type INTEGER NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "RevisionId TEXT, " +
                           "LastWriteTime TEXT NOT NULL, " +
                           "Size INTEGER NOT NULL, " +
                           "ContentVersion INTEGER NOT NULL, " +
                           "Status INTEGER NOT NULL)");

        if (!ColumnExists(connection, "AdapterTree", "VolumeId"))
        {
            connection.Execute("ALTER TABLE AdapterTree ADD COLUMN VolumeId INTEGER");
            connection.Execute("UPDATE AdapterTree SET VolumeId = 0 WHERE AltId IS NOT NULL");
        }

        if (!ColumnExists(connection, "AdapterTree", "RevisionId"))
        {
            connection.Execute("ALTER TABLE AdapterTree ADD COLUMN RevisionId TEXT");
        }

        connection.Execute("CREATE INDEX IF NOT EXISTS AdapterTree_Idx_ParentId ON AdapterTree(ParentId)");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS AdapterTree_Idx_VolumeIdAltId ON AdapterTree(VolumeId, AltId)");

        // Dropping old index used before adding VolumeId column
        connection.Execute("DROP INDEX IF EXISTS AdapterTree_Idx_AltId");
    }
}
