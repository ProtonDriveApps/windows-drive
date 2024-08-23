namespace ProtonDrive.Client.Contracts;

public sealed record UserResponse : ApiResponse
{
    private User? _user;

    public User User
    {
        get => _user ??= new User();
        init => _user = value;
    }
}
