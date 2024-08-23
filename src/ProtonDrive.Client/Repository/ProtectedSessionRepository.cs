using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Security.Cryptography;

namespace ProtonDrive.Client.Repository;

internal class ProtectedSessionRepository : IProtectedRepository<Session>
{
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly IRepository<Session> _origin;
    private readonly ILogger<ProtectedSessionRepository> _logger;

    public ProtectedSessionRepository(
        IDataProtectionProvider protectionProvider,
        IRepository<Session> origin,
        ILogger<ProtectedSessionRepository> logger)
    {
        _protectionProvider = protectionProvider;
        _origin = origin;
        _logger = logger;
    }

    public Session? Get()
    {
        return ToUnprotected(_origin.Get());
    }

    public void Set(Session? value)
    {
        _origin.Set(ToProtected(value));
    }

    private Session? ToProtected(Session? session)
    {
        if (session == null)
        {
            return session;
        }

        try
        {
            return session with
            {
                AccessToken = ToProtected(session.AccessToken),
                RefreshToken = ToProtected(session.RefreshToken),
            };
        }
        catch (CryptographicException)
        {
            _logger.LogError("Failed to protect Session");

            return null;
        }
    }

    private Session? ToUnprotected(Session? session)
    {
        if (session == null)
        {
            return session;
        }

        try
        {
            return session with
            {
                AccessToken = ToUnprotected(session.AccessToken),
                RefreshToken = ToUnprotected(session.RefreshToken),
            };
        }
        catch (CryptographicException)
        {
            _logger.LogError("Failed to unprotect Session");

            return null;
        }
    }

    private string ToProtected(string value)
    {
        return _protectionProvider.Protect(value);
    }

    private string ToUnprotected(string value)
    {
        return _protectionProvider.Unprotect(value);
    }
}
