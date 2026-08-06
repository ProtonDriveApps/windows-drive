using System.Text.Json;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Shared.Text.Serialization;

public sealed class JsonUtf8Serializer : IBinarySerializer, IThrowsExpectedExceptions
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new Base64JsonConverter() },
    };

    public T? Deserialize<T>(Stream stream)
    {
        return JsonSerializer.DeserializeAsync<T>(stream, _options).Result;
    }

    public void Serialize<T>(T? value, Stream stream)
    {
        JsonSerializer.SerializeAsync(stream, value, _options).Wait();
    }

    public bool IsExpectedException(Exception ex)
    {
        return ex is JsonException or ArgumentNullException;
    }
}
