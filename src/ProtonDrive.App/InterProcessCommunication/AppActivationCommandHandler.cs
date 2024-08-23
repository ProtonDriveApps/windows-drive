using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.InterProcessCommunication;

/// <summary>
/// Handles the app activation request from the second instance of the app.
/// </summary>
internal sealed class AppActivationCommandHandler : IpcMessageHandlerBase<object?>
{
    private readonly IApp _app;

    public AppActivationCommandHandler(IApp app)
        : base(IpcMessageType.AppActivationCommand)
    {
        _app = app;
    }

    public override async Task HandleAsync<T>(object? parameters, T responder, CancellationToken cancellationToken)
    {
        // Request to activate the app does not require parameters.

        // Activate the app and get the app window handle
        var windowHandle = await _app.ActivateAsync().ConfigureAwait(false);

        // Return the app window handle to the second instance of the app so that it could
        // bring that window to front of other app windows.
        await responder.Respond(windowHandle.ToInt64(), cancellationToken).ConfigureAwait(false);
    }
}
