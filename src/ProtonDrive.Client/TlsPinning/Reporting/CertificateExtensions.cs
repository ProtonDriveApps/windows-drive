using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProtonDrive.Client.TlsPinning.Reporting;

internal static class CertificateExtensions
{
    public static string ExportToPem(this X509Certificate cert)
    {
        var builder = new StringBuilder();

        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        builder.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END CERTIFICATE-----");

        return builder.ToString();
    }
}
