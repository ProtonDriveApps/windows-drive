using System.Collections.Immutable;

namespace ProtonDrive.Client.Sanitization;

public sealed record DocumentListResponse : ApiResponse
{
    private IImmutableList<DocumentIdentity>? _documents;

    public IImmutableList<DocumentIdentity> Documents
    {
        get => _documents ??= ImmutableList<DocumentIdentity>.Empty;
        init => _documents = value;
    }
}
