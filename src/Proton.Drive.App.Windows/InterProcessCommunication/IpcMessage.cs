using System.Text.Json.Nodes;

namespace Proton.Drive.App.Windows.InterProcessCommunication;

internal record IpcMessage
{
    public string? Type { get; set; }
    public JsonNode? Parameters { get; set; }
}
