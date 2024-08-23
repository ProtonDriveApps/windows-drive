using System;
using System.IO;
using System.Text.Json;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Shared.Text.Serialization;

public sealed class JsonUtf8Serializer : IBinarySerializer, IThrowsExpectedExceptions
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new Base64JsonConverter() }
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
