using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaStreamNet;

/// <summary>
/// Serializes <see cref="DateTime"/> as a Unix timestamp in milliseconds (long)
/// instead of an ISO 8601 string.
/// </summary>
public sealed class UnixMillisDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var millis = reader.GetInt64();
        return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var millis = new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeMilliseconds();
        writer.WriteNumberValue(millis);
    }

    /// <summary>Writes a DateTime property as Unix millis directly to a <see cref="Utf8JsonWriter"/>.</summary>
    public static void WritePropertyValue(Utf8JsonWriter writer, ReadOnlySpan<byte> propertyName, DateTime value)
    {
        var millis = new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeMilliseconds();
        writer.WriteNumber(propertyName, millis);
    }

    /// <summary>Reads a Unix millis timestamp from a <see cref="JsonElement"/>.</summary>
    public static DateTime ReadElement(JsonElement element)
    {
        var millis = element.GetInt64();
        return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
    }
}
