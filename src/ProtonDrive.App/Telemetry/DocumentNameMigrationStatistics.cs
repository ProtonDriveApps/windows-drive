using System.Threading;

namespace ProtonDrive.App.Telemetry;

public sealed class DocumentNameMigrationStatistics
{
    private int _numberOfMigrationsStarted;
    private int _numberOfMigrationsSkipped;
    private int _numberOfMigrationsCompleted;
    private int _numberOfMigrationsFailed;
    private int _numberOfDocumentRenamingAttempts;
    private int _numberOfNonMappedDocuments;
    private int _numberOfFailedDocumentRenamingAttempts;
    private int _numberOfRenamedDocuments;
    private int _numberOfDocumentsNotRequiringRename;

    public int NumberOfMigrationsStarted => _numberOfMigrationsStarted;

    public int NumberOfMigrationsSkipped => _numberOfMigrationsSkipped;

    public int NumberOfMigrationsCompleted => _numberOfMigrationsCompleted;

    public int NumberOfMigrationsFailed => _numberOfMigrationsFailed;

    public int NumberOfDocumentRenamingAttempts => _numberOfDocumentRenamingAttempts;

    public int NumberOfNonMappedDocuments => _numberOfNonMappedDocuments;

    public int NumberOfFailedDocumentRenamingAttempts => _numberOfFailedDocumentRenamingAttempts;

    public int NumberOfRenamedDocuments => _numberOfRenamedDocuments;

    public int NumberOfDocumentsNotRequiringRename => _numberOfDocumentsNotRequiringRename;

    public void IncrementNumberOfMigrationsStarted()
    {
        Interlocked.Increment(ref _numberOfMigrationsStarted);
    }

    public void IncrementNumberOfMigrationsSkipped()
    {
        Interlocked.Increment(ref _numberOfMigrationsSkipped);
    }

    public void IncrementNumberOfMigrationsCompleted()
    {
        Interlocked.Increment(ref _numberOfMigrationsCompleted);
    }

    public void IncrementNumberOfMigrationsFailed()
    {
        Interlocked.Increment(ref _numberOfMigrationsFailed);
    }

    public void IncrementNumberOfDocumentRenamingAttempts()
    {
        Interlocked.Increment(ref _numberOfDocumentRenamingAttempts);
    }

    public void IncrementNumberOfNonMappedDocuments()
    {
        Interlocked.Increment(ref _numberOfNonMappedDocuments);
    }

    public void IncrementNumberOfFailedDocumentRenamingAttempts()
    {
        Interlocked.Increment(ref _numberOfFailedDocumentRenamingAttempts);
    }

    public void IncrementNumberOfRenamedDocuments()
    {
        Interlocked.Increment(ref _numberOfRenamedDocuments);
    }

    public void IncrementNumberOfDocumentsNotRequiringRename()
    {
        Interlocked.Increment(ref _numberOfDocumentsNotRequiringRename);
    }
}
