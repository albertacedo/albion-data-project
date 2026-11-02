using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlbionMarket.Core.Models;

/// <summary>
/// The value can arrive as a JSON string ("3005") or as a number (3005),
/// depending on the source: the local client's HTTP ingest uses strings, the
/// public NATS feed sends numbers. This reads both.
/// </summary>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => "",
            _ => throw new JsonException($"No puc llegir {reader.TokenType} com a text.")
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
