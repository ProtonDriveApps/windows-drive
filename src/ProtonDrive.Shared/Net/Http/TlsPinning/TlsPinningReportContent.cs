using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace ProtonDrive.Shared.Net.Http.TlsPinning;

public sealed record TlsPinningReportContent(string HostName, X509Chain CertificateChain, IReadOnlyCollection<string> KnownPins);
