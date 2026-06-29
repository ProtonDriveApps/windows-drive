using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal sealed class ExtendedAttributesReader : IExtendedAttributesReader
{
    private readonly ICryptographyService _cryptographyService;
    private readonly ILogger<ExtendedAttributesReader> _logger;

    public ExtendedAttributesReader(ICryptographyService cryptographyService, ILogger<ExtendedAttributesReader> logger)
    {
        _cryptographyService = cryptographyService;
        _logger = logger;
    }

    public async Task<ExtendedAttributes?> ReadAsync(Link link, PgpPrivateKey nodeKey, CancellationToken cancellationToken)
    {
        try
        {
            if (link.ExtendedAttributes is null)
            {
                return null;
            }

            var signatureEmailAddress = link.FileProperties?.ActiveRevision?.SignatureEmailAddress;

            var decrypter = await _cryptographyService.CreateNodeNameAndKeyPassphraseDecrypterAsync(nodeKey, signatureEmailAddress, cancellationToken)
                .ConfigureAwait(false);

            var result = decrypter.GetDecryptingAndVerifyingStream(Encoding.ASCII.GetBytes(link.ExtendedAttributes));
            var extendedAttributes = JsonSerializer.Deserialize<ExtendedAttributes>(result.DecryptingStream);

            LogIfSignatureIsInvalid(result.GetVerificationStatus.Invoke(), link);

            ValidateSize(extendedAttributes);

            return extendedAttributes;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                "Extended attributes for LinkID={LinkId} and RevisionID={RevisionId} cannot be deserialized: {ErrorMessage}",
                link.Id,
                link.FileProperties?.ActiveRevision?.Id,
                ex.CombinedMessage());
            return null;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(
                "Extended attributes for LinkID={LinkId} and RevisionID={RevisionId} cannot be decrypted: {ErrorMessage}",
                link.Id,
                link.FileProperties?.ActiveRevision?.Id,
                ex.CombinedMessage());
            return null;
        }
    }

    private void LogIfSignatureIsInvalid(PgpVerificationStatus verificationStatus, Link link)
    {
        if (verificationStatus == PgpVerificationStatus.Ok)
        {
            return;
        }

        _logger.LogWarning(
            "Signature problem on extended attributes for LinkID={LinkId} and RevisionID={RevisionId}: {VerificationResultCode}",
            link.Id,
            link.FileProperties?.ActiveRevision?.Id,
            verificationStatus);
    }

    private void ValidateSize(ExtendedAttributes? extendedAttributes)
    {
        if (extendedAttributes?.Common?.Size is null)
        {
            return;
        }

        var sizeIsValid = extendedAttributes.Common.Size >= 0;

        if (sizeIsValid)
        {
            return;
        }

        _logger.LogWarning("Extended attributes contain an invalid size: {Size}", extendedAttributes.Common.Size);
        extendedAttributes.Common.Size = null;
    }
}
