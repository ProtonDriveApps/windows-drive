using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client.FileUploading;

internal class RevisionSealerFactory : IRevisionSealerFactory
{
    private readonly IFileRevisionUpdateApiClient _fileRevisionUpdateApiClient;
    private readonly IRevisionManifestCreator _revisionManifestCreator;

    public RevisionSealerFactory(
        IFileRevisionUpdateApiClient fileRevisionUpdateApiClient,
        IRevisionManifestCreator revisionManifestCreator)
    {
        _fileRevisionUpdateApiClient = fileRevisionUpdateApiClient;
        _revisionManifestCreator = revisionManifestCreator;
    }

    public IRevisionSealer Create(
        string shareId,
        string linkId,
        string revisionId,
        IPgpSignatureProducer signatureProducer,
        Address signatureAddress,
        IExtendedAttributesBuilder extendedAttributesBuilder)
    {
        return new RevisionSealer(
            shareId,
            linkId,
            revisionId,
            signatureProducer,
            signatureAddress,
            _revisionManifestCreator,
            extendedAttributesBuilder,
            _fileRevisionUpdateApiClient);
    }
}
