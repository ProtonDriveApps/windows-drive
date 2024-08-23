using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Devices;

namespace ProtonDrive.App.Devices;

internal sealed class ClientInstanceIdentityProvider : IClientInstanceIdentityProvider
{
    private readonly ClientInstanceSettings _settings;

    public ClientInstanceIdentityProvider(ClientInstanceSettings settings)
    {
        _settings = settings;
    }

    public string GetClientInstanceId() => _settings.ClientInstanceId;
}
