using System.Collections.Generic;

namespace ProtonDrive.Shared.Net.Http.TlsPinning;

public sealed class TlsPinningConfig
{
    public ICollection<string> PublicKeyHashes { get; } = [];
    public bool Enforce { get; internal set; }
    public bool SendReport { get; internal set; }
    public bool IgnoreRemoteCertificateErrors { get; private init; }

    /// <summary>
    /// Disables TLS pinning.
    /// </summary>
    /// <returns><see cref="TlsPinningConfig"/> that disables TLS pinning.</returns>
    public static TlsPinningConfig Disabled()
    {
        return new TlsPinningConfig();
    }

    /// <summary>
    /// Disables TLS pinning and ignores remote SSL certificate errors.
    /// </summary>
    /// <returns><see cref="TlsPinningConfig"/> that disables TLS pinning and ignores remote SSL certificate errors.</returns>
    public static TlsPinningConfig DisabledAndRemoteCertificateErrorsIgnored()
    {
        return new TlsPinningConfig
        {
            IgnoreRemoteCertificateErrors = true,
        };
    }

    /// <summary>
    /// Blocks communication.
    /// </summary>
    /// <returns><see cref="TlsPinningConfig"/> that blocks HTTP communication.</returns>
    public static TlsPinningConfig Blocking()
    {
        return new TlsPinningConfig
        {
            Enforce = true,
            SendReport = false,
        };
    }
}
