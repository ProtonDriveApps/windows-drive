using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Shared.Devices;

namespace Proton.Drive.Sdk.Sync.Agent.Devices;

internal sealed class ClientInstanceIdentityProvider : IClientInstanceIdentityProvider
{
    private readonly ClientInstanceSettings _settings;

    public ClientInstanceIdentityProvider(ClientInstanceSettings settings)
    {
        _settings = settings;
    }

    public string GetClientInstanceId() => _settings.ClientInstanceId;
}
