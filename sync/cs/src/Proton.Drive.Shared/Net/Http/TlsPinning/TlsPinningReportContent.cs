using System.Security.Cryptography.X509Certificates;

namespace Proton.Drive.Shared.Net.Http.TlsPinning;

public sealed record TlsPinningReportContent(string HostName, X509Chain CertificateChain, IReadOnlyCollection<string> KnownPins);
