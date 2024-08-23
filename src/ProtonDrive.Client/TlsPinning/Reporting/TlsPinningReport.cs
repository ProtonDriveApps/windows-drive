using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Net.Http.TlsPinning;

namespace ProtonDrive.Client.TlsPinning.Reporting;

internal sealed class TlsPinningReport
{
    public TlsPinningReport(TlsPinningReportContent content)
    {
        DateTime = System.DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
        Hostname = content.HostName;
        Port = 443;
        ValidatedCertificateChain = CertificateChain(content.CertificateChain);
        KnownPins = content.KnownPins;
    }

    [JsonPropertyName("date-time")]
    public string DateTime { get; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; }

    [JsonPropertyName("port")]
    public int Port { get; }

    [JsonPropertyName("validated-certificate-chain")]
    public IReadOnlyList<string> ValidatedCertificateChain { get; }

    [JsonPropertyName("known-pins")]
    public IReadOnlyCollection<string> KnownPins { get; }

    private List<string> CertificateChain(X509Chain chain)
    {
        var list = new List<string>();

        foreach (var element in chain.ChainElements)
        {
            list.Add(element.Certificate.ExportToPem());
        }

        return list;
    }
}
