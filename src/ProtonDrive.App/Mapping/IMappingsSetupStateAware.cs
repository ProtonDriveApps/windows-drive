using System.Threading.Tasks;

namespace ProtonDrive.App.Mapping;

public interface IMappingsSetupStateAware
{
    void OnMappingsSetupStateChanged(MappingsSetupState value);
    Task OnMappingsSettingUpAsync() => Task.CompletedTask;
}
