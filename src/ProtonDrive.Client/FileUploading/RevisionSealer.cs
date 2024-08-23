using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client.FileUploading;

internal sealed class RevisionSealer : IRevisionSealer
{
    private readonly string _shareId;
    private readonly string _fileId;
    private readonly string _revisionId;
    private readonly IPgpSignatureProducer _signatureProducer;
    private readonly Address _signatureAddress;
    private readonly IRevisionManifestCreator _revisionManifestCreator;
    private readonly IExtendedAttributesBuilder _extendedAttributesBuilder;
    private readonly IFileRevisionUpdateApiClient _fileRevisionUpdateApiClient;

    public RevisionSealer(
        string shareId,
        string fileId,
        string revisionId,
        IPgpSignatureProducer signatureProducer,
        Address signatureAddress,
        IRevisionManifestCreator revisionManifestCreator,
        IExtendedAttributesBuilder extendedAttributesBuilder,
        IFileRevisionUpdateApiClient fileRevisionUpdateApiClient)
    {
        _fileRevisionUpdateApiClient = fileRevisionUpdateApiClient;
        _signatureProducer = signatureProducer;
        _shareId = shareId;
        _fileId = fileId;
        _revisionId = revisionId;
        _signatureAddress = signatureAddress;
        _revisionManifestCreator = revisionManifestCreator;
        _extendedAttributesBuilder = extendedAttributesBuilder;
    }

    public async Task SealRevisionAsync(IReadOnlyCollection<UploadedBlock> blocks, CancellationToken cancellationToken)
    {
        var manifest = _revisionManifestCreator.CreateManifest(blocks);

        var manifestSignature = _signatureProducer.SignWithArmor(manifest);

        _extendedAttributesBuilder.BlockSizes = blocks.Where(block => !block.IsThumbnail).Select(block => block.NumberOfPlainDataBytesRead);

        var extendedAttributes = await _extendedAttributesBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);

        var revisionUpdateParameters = new RevisionUpdateParameters(manifestSignature, _signatureAddress.EmailAddress, extendedAttributes);

        await _fileRevisionUpdateApiClient
            .UpdateRevisionAsync(_shareId, _fileId, _revisionId, revisionUpdateParameters, cancellationToken)
            .ThrowOnFailure()
            .ConfigureAwait(false);
    }
}
