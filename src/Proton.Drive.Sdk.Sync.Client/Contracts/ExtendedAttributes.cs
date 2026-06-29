using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record ExtendedAttributes(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CommonExtendedAttributes? Common,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    GeoLocationExtendedAttributes? Location = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CameraExtendedAttributes? Camera = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    MediaExtendedAttributes? Media = null);
