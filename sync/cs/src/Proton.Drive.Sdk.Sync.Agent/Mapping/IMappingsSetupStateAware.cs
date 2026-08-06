namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public interface IMappingsSetupStateAware
{
    /// <summary>
    /// Notifies folder mappings setup state change.
    /// </summary>
    /// <param name="value">The <see cref="MappingsSetupState"/> value.</param>
    void OnMappingsSetupStateChanged(MappingsSetupState value);

    /// <summary>
    /// Folder mappings setup is about to start. It's time to check if any mappings you're using have been deleted,
    /// as you'll need to stop using them before continuing with the mappings setup.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnMappingsSettingUpAsync() => Task.CompletedTask;
}
