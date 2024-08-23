using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proton.Security.Cryptography;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.RemoteNodes;

internal sealed class ExtendedAttributesReader : IExtendedAttributesReader
{
    private readonly ICryptographyService _cryptographyService;
    private readonly ILogger<ExtendedAttributesReader> _logger;

    public ExtendedAttributesReader(ICryptographyService cryptographyService, ILogger<ExtendedAttributesReader> logger)
    {
        _cryptographyService = cryptographyService;
        _logger = logger;
    }

    public async Task<ExtendedAttributes?> ReadAsync(Link link, PrivatePgpKey nodeKey, CancellationToken cancellationToken)
    {
        try
        {
            if (link.ExtendedAttributes is null)
            {
                return default;
            }

            var signatureEmailAddress = link.FileProperties?.ActiveRevision?.SignatureEmailAddress ?? link.SignatureEmailAddress;

            var decrypter = await _cryptographyService.CreateNodeNameAndKeyPassphraseDecrypterAsync(nodeKey, signatureEmailAddress, cancellationToken)
                .ConfigureAwait(false);

            await using var messageSource = new PgpMessageSource(new AsciiStream(link.ExtendedAttributes), PgpArmoring.Ascii);
            var result = decrypter.GetDecryptingAndVerifyingStream(messageSource);
            var extendedAttributes = JsonSerializer.Deserialize<ExtendedAttributes>(result.Stream);

            LogIfSignatureIsInvalid(result.VerificationTask, link);

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
            return default;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(
                "Extended attributes for LinkID={LinkId} and RevisionID={RevisionId} cannot be decrypted: {ErrorMessage}",
                link.Id,
                link.FileProperties?.ActiveRevision?.Id,
                ex.CombinedMessage());
            return default;
        }
    }

    private void LogIfSignatureIsInvalid(Task<VerificationVerdict> verificationVerdictTask, Link link)
    {
        var result = verificationVerdictTask.Result;

        if (result == VerificationVerdict.ValidSignature)
        {
            return;
        }

        _logger.LogWarning(
            "Signature problem on extended attributes for LinkID={LinkId} and RevisionID={RevisionId}: {VerificationResultCode}",
            link.Id,
            link.FileProperties?.ActiveRevision?.Id,
            result);
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
