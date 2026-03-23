using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaStreamNet;

/// <summary>
/// Serializes <see cref="Guid"/> as a base64 string (22 chars) instead of the
/// standard 36-char hex-with-dashes format.
/// </summary>
public sealed class Base64GuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString()!;
        Span<byte> bytes = stackalloc byte[16];
        Convert.TryFromBase64String(str, bytes, out _);
        return new Guid(bytes);
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);
        writer.WriteStringValue(Convert.ToBase64String(bytes));
    }

    /// <summary>Writes a Guid property as base64 directly to a <see cref="Utf8JsonWriter"/>.</summary>
    public static void WritePropertyValue(Utf8JsonWriter writer, ReadOnlySpan<byte> propertyName, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);
        writer.WriteString(propertyName, Convert.ToBase64String(bytes));
    }

    /// <summary>Reads a base64-encoded Guid from a <see cref="JsonElement"/>.</summary>
    public static Guid ReadElement(JsonElement element)
    {
        var str = element.GetString()!;
        Span<byte> bytes = stackalloc byte[16];
        Convert.TryFromBase64String(str, bytes, out _);
        return new Guid(bytes);
    }
}
