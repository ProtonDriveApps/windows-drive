namespace Proton.Drive.Native.Authentication.Contracts;

internal sealed class PublicKeyCredentialRequestOptions
{
    public string? RpId { get; init; }
    public required IReadOnlyList<byte> Challenge { get; init; }
    public string? UserVerification { get; init; }
    public uint Timeout { get; init; }
    public IReadOnlyList<PublicKeyCredentialDescriptor> AllowCredentials { get; init; } = [];
}
