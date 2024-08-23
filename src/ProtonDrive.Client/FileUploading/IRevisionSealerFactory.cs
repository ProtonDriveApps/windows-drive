using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client.FileUploading;

internal interface IRevisionSealerFactory
{
    IRevisionSealer Create(
        string shareId,
        string linkId,
        string revisionId,
        IPgpSignatureProducer signatureProducer,
        Address signatureAddress,
        IExtendedAttributesBuilder extendedAttributesBuilder);
}
