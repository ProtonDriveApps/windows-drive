namespace ProtonDrive.App.Health;

internal enum FileConsistencyGuardStatus
{
    NotStarted = 0,
    NotApplicable = 2,
    Applicable = 3,
    Started = 5,
    Initialized = 7,
    Completed = 11,
    Finished = 13,
}
