using System.Data;
using Dapper;
using Proton.Drive.Sdk.Sync.DataAccess.Repositories;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.Changes;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.DataAccess.Databases;

public class SyncEngineDatabase : Database
{
    private static readonly IEnumerable<string> FileSystemNodeExtraColumns = new[]
    {
        nameof(IFileSystemNodeModel<long>.Type),
        nameof(IFileSystemNodeModel<long>.Name),
        nameof(IFileSystemNodeModel<long>.ContentVersion),
    };

    public SyncEngineDatabase(DatabaseConfig config)
        : base(config)
    {
        SyncedTreeRepository = new AltIdentifiableTreeNodeRepository<SyncedTreeNodeModel<long>, long>(
            this,
            "SyncedTree",
            FileSystemNodeExtraColumns);

        RemoteUpdateTreeRepository = new TreeNodeRepository<UpdateTreeNodeModel<long>>(
            this,
            "RemoteUpdateTree",
            FileSystemNodeExtraColumns
                .Append(nameof(UpdateTreeNodeModel<long>.Status)));

        LocalUpdateTreeRepository = new TreeNodeRepository<UpdateTreeNodeModel<long>>(
            this,
            "LocalUpdateTree",
            FileSystemNodeExtraColumns
                .Append(nameof(UpdateTreeNodeModel<long>.Status)));

        PropagationTreeRepository = new AltIdentifiableTreeNodeRepository<PropagationTreeNodeModel<long>, long>(
            this,
            "PropagationTree",
            FileSystemNodeExtraColumns
                .Append(nameof(PropagationTreeNodeModel<long>.RemoteStatus))
                .Append(nameof(PropagationTreeNodeModel<long>.LocalStatus))
                .Append(nameof(PropagationTreeNodeModel<long>.Backup)));

        LocalSyncedUpdateRepository = new TreeChangeRepository(this, "LocalSyncedUpdates");
    }

    public IAltIdentifiableTreeNodeRepository<SyncedTreeNodeModel<long>, long, long> SyncedTreeRepository { get; }
    public ITreeNodeRepository<UpdateTreeNodeModel<long>, long> LocalUpdateTreeRepository { get; }
    public ITreeNodeRepository<UpdateTreeNodeModel<long>, long> RemoteUpdateTreeRepository { get; }
    public IAltIdentifiableTreeNodeRepository<PropagationTreeNodeModel<long>, long, long> PropagationTreeRepository { get; }
    public ITreeChangeRepository<long> LocalSyncedUpdateRepository { get; }

    protected override void SetupDatabase(IDbConnection connection)
    {
        base.SetupDatabase(connection);

        // SyncedTree
        connection.Execute("CREATE TABLE IF NOT EXISTS SyncedTree(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "ParentId INTEGER NOT NULL REFERENCES SyncedTree(Id), " +
                           "AltId INTEGER NOT NULL, " +
                           "Type INTEGER NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "ContentVersion INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS SyncedTree_Idx_ParentId ON SyncedTree(ParentId)");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS SyncedTree_Idx_AltId ON SyncedTree(AltId)");

        // RemoteUpdateTree
        connection.Execute("CREATE TABLE IF NOT EXISTS RemoteUpdateTree(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "ParentId INTEGER NOT NULL REFERENCES RemoteUpdateTree(Id), " +
                           "Type INTEGER NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "ContentVersion INTEGER NOT NULL, " +
                           "Status INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS RemoteUpdateTree_Idx_ParentId ON RemoteUpdateTree(ParentId)");

        // LocalUpdateTree
        connection.Execute("CREATE TABLE IF NOT EXISTS LocalUpdateTree(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "ParentId INTEGER NOT NULL REFERENCES LocalUpdateTree(Id), " +
                           "Type INTEGER NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "ContentVersion INTEGER NOT NULL, " +
                           "Status INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS LocalUpdateTree_Idx_ParentId ON LocalUpdateTree(ParentId)");

        // PropagationTree
        connection.Execute("CREATE TABLE IF NOT EXISTS PropagationTree(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "ParentId INTEGER NOT NULL REFERENCES PropagationTree(Id), " +
                           "AltId INTEGER NOT NULL, " +
                           "Type INTEGER NOT NULL, " +
                           "Name TEXT NOT NULL, " +
                           "ContentVersion INTEGER NOT NULL, " +
                           "RemoteStatus INTEGER NOT NULL, " +
                           "LocalStatus INTEGER NOT NULL, " +
                           "Backup INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS PropagationTree_Idx_ParentId ON PropagationTree(ParentId)");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS PropagationTree_Idx_AltId ON PropagationTree(AltId)");

        // LocalSyncedUpdates
        connection.Execute("CREATE TABLE IF NOT EXISTS LocalSyncedUpdates(" +
                           "Id INTEGER NOT NULL PRIMARY KEY ASC, " +
                           "OperationType INTEGER NOT NULL, " +
                           "NodeId INTEGER NOT NULL, " +
                           "NodeParentId INTEGER NOT NULL, " +
                           "NodeType INTEGER NOT NULL, " +
                           "NodeName TEXT NOT NULL, " +
                           "NodeContentVersion INTEGER NOT NULL)");

        connection.Execute("CREATE INDEX IF NOT EXISTS LocalSyncedUpdates_Idx_NodeId ON LocalSyncedUpdates(NodeId)");
    }
}
