using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

internal sealed class BearerAuthorizationHeaderAttribute : HeadersAttribute
{
    public BearerAuthorizationHeaderAttribute()
        : base("Authorization: Bearer")
    {
    }
}
