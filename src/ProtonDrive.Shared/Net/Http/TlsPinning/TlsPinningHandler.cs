using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ProtonDrive.Shared.Net.Http.TlsPinning;

/// <summary>
/// Validates the HTTPS server certificate subject public key according to the provided configuration.
/// Optionally, reports validation errors.
/// </summary>
internal sealed class TlsPinningHandler
{
    private readonly TlsPinningConfig _config;
    private readonly Lazy<ITlsPinningReportClient> _reportClient;
    private readonly TlsPinningPolicy _policy;

    public TlsPinningHandler(TlsPinningConfig config, Func<ITlsPinningReportClient> reportClientFactory)
    {
        _config = config;

        _reportClient = new Lazy<ITlsPinningReportClient>(reportClientFactory);
        _policy = new TlsPinningPolicy(config);
    }

    public bool ValidateRemoteCertificate(string hostName, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null || chain == null)
        {
            return false;
        }

        var valid = _policy.IsValid(certificate);

        // If the certificate subject public key does not match any of configured public key hashes
        // and sending the error report is configured, we send the report.
        if (!valid && _config.SendReport)
        {
            var knownPins = _config.PublicKeyHashes.ToList();
            _reportClient.Value.SendAsync(new TlsPinningReportContent(hostName, chain, knownPins));
        }

        // The certificate having a valid subject public key is accepted despite other SSL policy errors.
        if (valid)
        {
            return true;
        }

        // If the certificate subject public key does not match any of configured public key hashes,
        // it is still accepted if the pinning is not enforced and there are no other SSL policy errors.
        return !_config.Enforce && (sslPolicyErrors == SslPolicyErrors.None || _config.IgnoreRemoteCertificateErrors);
    }
}
