using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using ProtonDrive.DataAccess.Repositories;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLinking;
using ProtonDrive.Sync.Adapter.Trees.Dirty;
using ProtonDrive.Sync.Adapter.Trees.StateMaintenance;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.DataAccess.Databases;

public class AdapterDatabase : Database
{
    protected static readonly IEnumerable<string> FileSystemNodeExtraColumns =
    [
        nameof(IFileSystemNodeModel<long>.Type),
        nameof(IFileSystemNodeModel<long>.Name),
        nameof(IFileSystemNodeModel<long>.ContentVersion),
    ];

    protected static readonly IEnumerable<string> AdapterNodeExtraColumns =
    [
        nameof(AdapterTreeNodeModel<long, long>.RevisionId),
        nameof(AdapterTreeNodeModel<long, long>.LastWriteTime),
        nameof(AdapterTreeNodeModel<long, long>.Size),
        nameof(AdapterTreeNodeModel<long, long>.Status),
    ];

    public AdapterDatabase(DatabaseConfig config, bool isSyncStateMaintenanceSupported)
        : base(config)
    {
        NodeLinkRepository = new NodeLinkRepository(this, "NodeLinks");

        DirtyTreeRepository = new TreeNodeRepository<DirtyTreeNodeModel<long>>(
            this,
            "DirtyTree",
            FileSystemNodeExtraColumns
                .Append(nameof(DirtyTreeNodeModel<long>.Status)));

        DetectedUpdateRepository = new TreeChangeRepository(this, "DetectedUpdates");

        if (isSyncStateMaintenanceSupported)
        {
            StateMaintenanceTreeRepository = new TreeNodeRepository<StateMaintenanceTreeNodeModel<long>>(
                this,
                "StateMaintenanceTree",
                FileSystemNodeExtraColumns
                    .Append(nameof(StateMaintenanceTreeNodeModel<long>.Status)));
        }
    }

    public INodeLinkRepository<long> NodeLinkRepository { get; }
    public ITreeNodeRepository<DirtyTreeNodeModel<long>, long> DirtyTreeRepository { get; }
    public ITreeChangeRepository<long> DetectedUpdateRepository { get; }
    public ITreeNodeRepository<StateMaintenanceTreeNodeModel<long>, long>? StateMaintenanceTreeRepository { get; }

    protected override void SetupDatabase(IDbConnection connection)
    {
        base.SetupDatabase(connection);

        // Node links
        connection.Execute(
            "CREATE TABLE IF NOT EXISTS NodeLinks(" +
            "SourceId INTEGER NOT NULL REFERENCES AdapterTree(Id), " +
            "Type INTEGER NOT NULL, " +
            "DestinationId INTEGER NOT NULL REFERENCES AdapterTree(Id), " +
            "PRIMARY KEY (SourceId, Type))");

        connection.Execute("CREATE INDEX IF NOT EXISTS NodeLinks_Idx_DestinationId_Type ON NodeLinks(DestinationId, Type)");

        // Dirty Tree
        connection.Execute("CREATE TABLE IF NOT EXISTS DirtyTree(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "ParentId INTEGER NOT NULL REFERENCES DirtyTree(Id), " +
                           "Type INTEGER NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "ContentVersion INTEGER NOT NULL, " +
                           "Status INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS DirtyTree_Idx_ParentId ON DirtyTree(ParentId)");

        // Detected Updates
        connection.Execute("CREATE TABLE IF NOT EXISTS DetectedUpdates(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "OperationType INTEGER NOT NULL, " +
                           "NodeId INTEGER NOT NULL, " +
                           "NodeParentId INTEGER NOT NULL, " +
                           "NodeType INTEGER NOT NULL, " +
                           "NodeName TEXT NOT NULL, " +
                           "NodeContentVersion INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS DetectedUpdates_Idx_NodeId ON DetectedUpdates(NodeId)");

        if (StateMaintenanceTreeRepository != null)
        {
            // State Maintenance Tree
            connection.Execute("CREATE TABLE IF NOT EXISTS StateMaintenanceTree(" +
                               "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                               "ParentId INTEGER NOT NULL REFERENCES StateMaintenanceTree(Id), " +
                               "Type INTEGER NOT NULL, " +
                               "Name TEXT NOT NULL, " +
                               "ContentVersion INTEGER NOT NULL, " +
                               "Status INTEGER NOT NULL)");

            connection.Execute("CREATE INDEX IF NOT EXISTS StateMaintenanceTree_Idx_ParentId ON StateMaintenanceTree(ParentId)");
        }
    }

    protected static bool ColumnExists(IDbConnection connection, string tableName, string columnName)
    {
        return connection.QueryFirstOrDefault<int>($"SELECT COUNT(1) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'") > 0;
    }
}
