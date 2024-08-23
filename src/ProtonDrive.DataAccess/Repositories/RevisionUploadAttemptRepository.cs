using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ProtonDrive.Sync.Adapter;

namespace ProtonDrive.DataAccess.Repositories;

public sealed class RevisionUploadAttemptRepository : IRevisionUploadAttemptRepository
{
    private const string TableName = "RevisionUploadAttempt";

    private readonly string _getSql;
    private readonly string _addSql;
    private readonly string _deleteSql;
    private readonly IConnectionProvider _database;

    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public RevisionUploadAttemptRepository(IConnectionProvider database)
    {
        _database = database;

        _getSql = "SELECT LinkId, RevisionId FROM " + TableName + " WHERE ParentLinkId = @ParentLinkId AND Name = @Name";
        _addSql = "INSERT INTO " + TableName + " (ParentLinkId, Name, LinkId, RevisionId) VALUES (@ParentLinkId, @Name, @LinkId, @RevisionId)";
        _deleteSql = "DELETE FROM " + TableName + " WHERE ParentLinkId = @ParentLinkId AND Name = @Name";
    }

    public async Task<(string LinkId, string? RevisionId)?> GetAsync(string parentLinkId, string name)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            return await _database.Connection.QuerySingleOrDefaultAsync<(string LinkId, string RevisionId)?>(
                    _getSql,
                    new { ParentLinkId = parentLinkId, Name = name }).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddAsync(string parentLinkId, string name, string linkId, string? revisionId)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            await _database.Connection.ExecuteAsync(_addSql, new { ParentLinkId = parentLinkId, Name = name, LinkId = linkId, RevisionId = revisionId })
                .ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteAsync(string parentLinkId, string name)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            await _database.Connection.ExecuteAsync(_deleteSql, new { ParentLinkId = parentLinkId, Name = name }).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
