using System.Text.Json.Nodes;

namespace Proton.Drive.App.InterProcessCommunication;

public interface IIpcMessageHandler
{
    string MessageType { get; }

    Task HandleAsync<T>(JsonNode? parametersNode, T responder, CancellationToken cancellationToken) where T : IIpcResponder;
}
