using System.Text.Json;
using System.Text.Json.Nodes;

namespace Proton.Drive.App.InterProcessCommunication;

internal abstract class IpcMessageHandlerBase<TParameters> : IIpcMessageHandler
{
    protected IpcMessageHandlerBase(string messageType)
    {
        MessageType = messageType;
    }

    public string MessageType { get; }

    Task IIpcMessageHandler.HandleAsync<T>(JsonNode? parametersNode, T responder, CancellationToken cancellationToken)
    {
        var parameters = parametersNode.Deserialize<TParameters>();

        return HandleAsync(parameters, responder, cancellationToken);
    }

    public abstract Task HandleAsync<T>(TParameters? parameters, T responder, CancellationToken cancellationToken)
        where T : IIpcResponder;
}
