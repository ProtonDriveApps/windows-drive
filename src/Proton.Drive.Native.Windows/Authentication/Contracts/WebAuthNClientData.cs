namespace Proton.Drive.Native.Authentication.Contracts;

internal sealed class WebAuthNClientData
{
    public required string Type { get; init; }
    public required string Challenge { get; init; }
    public required string Origin { get; init; }
    public required bool CrossOrigin { get; init; }
}
