using Dapper;
using Microsoft.Data.Sqlite;
using Proton.Drive.Sdk.Sync.Shared.Health;
using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.DataAccess.Repositories;

public sealed class FileConsistencyGuardRepository
{
    private const string FilesTableName = "Files";

    private readonly IConnectionProvider _database;

    public FileConsistencyGuardRepository(IConnectionProvider database)
    {
        _database = database;
    }

    public async Task<IEnumerable<FileConsistencyGuardFileModel>> GetFilesByStatusAsync(FileConsistencyGuardFileStatus status, bool includeDisabledRoots)
    {
        const string sqlIncludingDisabledRoots = $"SELECT * FROM {FilesTableName} WHERE Status = @Status";
        const string sqlExcludingDisabledRoots = $"SELECT * FROM {FilesTableName} WHERE Status = @Status AND Error <> 1 ";

        var sql = includeDisabledRoots ? sqlIncludingDisabledRoots : sqlExcludingDisabledRoots;

        return await _database.Connection.QueryAsync<FileConsistencyGuardFileModel>(sql, new { Status = status }).ConfigureAwait(false);
    }

    public async Task<IEnumerable<FileConsistencyGuardFileModel>> GetFilesByStatusesAsync(FileConsistencyGuardFileStatus[] statuses, bool includeDisabledRoots)
    {
        const string sqlIncludingDisabledRoots = $"SELECT * FROM {FilesTableName} WHERE Status IN @Statuses";
        const string sqlExcludingDisabledRoots = $"SELECT * FROM {FilesTableName} WHERE Status IN @Statuses AND Error <> 1 ";

        var sql = includeDisabledRoots ? sqlIncludingDisabledRoots : sqlExcludingDisabledRoots;

        return await _database.Connection.QueryAsync<FileConsistencyGuardFileModel>(sql, new { Statuses = statuses }).ConfigureAwait(false);
    }

    public async Task AddFileAsync(FileConsistencyGuardFileModel file)
    {
        const string sql =
            $"""
             INSERT INTO {FilesTableName}
             (Id, LocalRootId, RemoteRootId, LocalId, RemoteId, Name, LocalSize, RemoteSize, RemotePlainSize, RemoteSizeOnStorage, LocalLastWriteTime, RemoteLastWriteTime, RevisionId, ContentVersion, LocalHash, RemoteHash, TrailingZeroBytesLength, Status, Reason, DownloadReason, Error)
             VALUES (@Id, @LocalRootId, @RemoteRootId, @LocalId, @RemoteId, @Name, @LocalSize, @RemoteSize, @RemotePlainSize, @RemoteSizeOnStorage, @LocalLastWriteTime, @RemoteLastWriteTime, @RevisionId, @ContentVersion, @LocalHash, @RemoteHash, @TrailingZeroBytesLength, @Status, @Reason, @DownloadReason, @Error)
             """;

        try
        {
            var rowsAffected = await _database.Connection.ExecuteAsync(sql, file).ConfigureAwait(false);

            if (rowsAffected != 1)
            {
                throw new TreeException($"Inserting file consistency guard file with Id {file.Id} failed");
            }
        }
        catch (SqliteException ex) when (ex is { SqliteErrorCode: 19, SqliteExtendedErrorCode: 1555 })
        {
            // Primary key constraint failed
            throw new TreeException($"File consistency guard file with Id {file.Id} already exists", ex);
        }
    }

    public async Task UpdateFilesAsync(ICollection<FileConsistencyGuardFileModel> files)
    {
        const string sql =
            $"""
             UPDATE {FilesTableName} SET
                 LocalRootId = @LocalRootId,
                 RemoteRootId = @RemoteRootId,
                 LocalId = @LocalId,
                 RemoteId = @RemoteId,
                 Name = @Name,
                 LocalSize = @LocalSize,
                 RemoteSize = @RemoteSize,
                 RemotePlainSize = @RemotePlainSize,
                 RemoteSizeOnStorage = @RemoteSizeOnStorage,
                 LocalLastWriteTime = @LocalLastWriteTime,
                 RemoteLastWriteTime = @RemoteLastWriteTime,
                 RevisionId = @RevisionId,
                 ContentVersion = @ContentVersion,
                 LocalHash = @LocalHash,
                 RemoteHash = @RemoteHash,
                 TrailingZeroBytesLength = @TrailingZeroBytesLength,
                 Status = @Status,
                 Reason = @Reason,
                 DownloadReason = @DownloadReason,
                 Error = @Error
             WHERE Id = @Id
             """;

        var rowsAffected = await _database.Connection.ExecuteAsync(sql, files).ConfigureAwait(false);

        if (rowsAffected != files.Count)
        {
            throw new TreeException("Updating file consistency guard files failed");
        }
    }

    public async Task ClearFilesAsync()
    {
        const string sql = $"DELETE FROM {FilesTableName}";

        await _database.Connection.ExecuteAsync(sql).ConfigureAwait(false);
    }

    public async Task<IEnumerable<FileConsistencyGuardFileStatisticsEntry>> GetFileStatisticsAsync()
    {
        const string sql =
            $"""
             SELECT 
             COUNT(Id) AS NumberOfFiles,
             ROUND(SUM(RemoteSize)/1024/1024/1024.0, 3) AS TotalFileSizeInGigaBytes,
             SUM((RemoteSize + (4*1024*1024) - 1) / (4*1024*1024)) AS NumberOfBlocks,
             Status,
             Reason,
             DownloadReason,
             Error
             FROM {FilesTableName}
             GROUP BY Status, Reason, DownloadReason, Error
             ORDER BY Status, Reason, DownloadReason, Error;
             """;

        return await _database.Connection.QueryAsync<FileConsistencyGuardFileStatisticsEntry>(sql).ConfigureAwait(false);
    }
}
