using Proton.Drive.App.Docs;

namespace Proton.Drive.App.InterProcessCommunication;

/// <summary>
/// Handles the request to open a document.
/// </summary>
internal sealed class OpenDocumentCommandHandler : IpcMessageHandlerBase<string>
{
    private readonly DocumentOpener _documentOpener;

    public OpenDocumentCommandHandler(DocumentOpener documentOpener)
        : base(IpcMessageType.OpenDocumentCommand)
    {
        _documentOpener = documentOpener;
    }

    public override async Task HandleAsync<T>(string? path, T responder, CancellationToken cancellationToken)
    {
        if (path is null)
        {
            return;
        }

        await _documentOpener.TryOpenAsync(path, CancellationToken.None).ConfigureAwait(false);
    }
}
