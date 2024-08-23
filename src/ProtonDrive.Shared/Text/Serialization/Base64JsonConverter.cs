using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProtonDrive.Shared.Text.Serialization;

public sealed class Base64JsonConverter : JsonConverter<ReadOnlyMemory<byte>>
{
    public override ReadOnlyMemory<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                $"Unexpected token type '{reader.TokenType}', expected '{nameof(JsonTokenType.String)}'");
        }

        return reader.GetBytesFromBase64();
    }

    public override void Write(Utf8JsonWriter writer, ReadOnlyMemory<byte> value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.Span);
    }
}
