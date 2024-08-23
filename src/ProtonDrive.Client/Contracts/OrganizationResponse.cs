namespace ProtonDrive.Client.Contracts;

public sealed record OrganizationResponse : ApiResponse
{
    private Organization? _organization;

    public Organization Organization
    {
        get => _organization ??= new Organization();
        init => _organization = value;
    }
}
