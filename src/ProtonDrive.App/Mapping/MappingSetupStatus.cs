namespace ProtonDrive.App.Mapping;

public enum MappingSetupStatus
{
    /// <summary>
    /// Account hasn't been set up.
    /// </summary>
    None,

    /// <summary>
    /// Setting up sync folder mappings.
    /// </summary>
    SettingUp,

    /// <summary>
    /// Setting up sync folder mappings has partially succeeded.
    /// </summary>
    PartiallySucceeded,

    /// <summary>
    /// Setting up sync folder mappings has fully succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Setting up sync folder mappings has failed. See <see cref="MappingErrorCode"/> for a list of possible errors.
    /// </summary>
    Failed,
}
