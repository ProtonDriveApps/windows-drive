using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client.FileUploading;

internal sealed class ExtendedAttributesBuilder : IExtendedAttributesBuilder
{
    private readonly ICryptographyService _cryptographyService;
    private readonly ILogger<ExtendedAttributesBuilder> _logger;

    public ExtendedAttributesBuilder(ICryptographyService cryptographyService, ILogger<ExtendedAttributesBuilder> logger)
    {
        _cryptographyService = cryptographyService;
        _logger = logger;
    }

    public PublicPgpKey? NodeKey { get; init; }
    public Address? SignatureAddress { get; init; }

    public long? Size { get; set; }
    public DateTime? LastWriteTime { get; set; }
    public IEnumerable<int>? BlockSizes { get; set; }

    public async Task<string?> BuildAsync(CancellationToken cancellationToken)
    {
        if (NodeKey is null)
        {
            throw new InvalidOperationException($"{nameof(NodeKey)} is required to encrypt extended attributes");
        }

        if (SignatureAddress is null)
        {
            throw new InvalidOperationException($"{nameof(SignatureAddress)} is required to encrypt extended attributes");
        }

        if (Size is null)
        {
            throw new InvalidOperationException($"{nameof(Size)} is required to encrypt extended attributes");
        }

        if (LastWriteTime is null)
        {
            throw new InvalidOperationException($"{nameof(LastWriteTime)} is required to encrypt extended attributes");
        }

        if (BlockSizes is null)
        {
            throw new InvalidOperationException($"{nameof(BlockSizes)} is required to encrypt extended attributes");
        }

        try
        {
            var encrypter = _cryptographyService.CreateExtendedAttributesEncrypter(NodeKey, SignatureAddress);

            var extendedAttributes = new ExtendedAttributes(new CommonExtendedAttributes
            {
                Size = Size,
                LastWriteTime = LastWriteTime,
                BlockSizes = BlockSizes,
            });

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(extendedAttributes);

            var jsonStream = new MemoryStream(jsonBytes);

            await using var plainDataSource = new PlainDataSource(jsonStream);

            var messageStream = encrypter.GetEncryptingAndSigningStream(plainDataSource, PgpArmoring.Ascii, PgpCompression.Deflate);

            using var messageStreamReader = new StreamReader(messageStream, Encoding.ASCII);

            var result = await messageStreamReader.ReadToEndAsync().ConfigureAwait(false);

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The creation of extended attributes failed: {Message}", exception.Message);
            return default;
        }
    }
}
