using Refit;

namespace ProtonDrive.Client;

internal sealed class BearerAuthorizationHeaderAttribute : HeadersAttribute
{
    public BearerAuthorizationHeaderAttribute()
        : base("Authorization: Bearer")
    {
    }
}
