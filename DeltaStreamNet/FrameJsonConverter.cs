using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaStreamNet;

internal class FrameJsonConverter<T> : JsonConverter<Frame<T>>
{
    private readonly Type _patchType;

    public FrameJsonConverter(Type patchType)
    {
        _patchType = patchType;
    }

    public override Frame<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var frameType = (FrameType)root.GetProperty("f").GetByte();
        var uuid = root.GetProperty("u").GetGuid();
        var version = root.GetProperty("v").GetUInt64();
        var timestamp = root.GetProperty("t").GetDateTime();

        if (frameType == FrameType.Key)
        {
            var value = root.GetProperty("d").Deserialize<T>(options)!;
            return new KeyFrame<T>
            {
                Type = FrameType.Key,
                EncoderUuid = uuid,
                Version = version,
                Timestamp = timestamp,
                Value = value
            };
        }
        else
        {
            var patch = (IDeltaPatch<T>)root.GetProperty("p").Deserialize(_patchType, options)!;
            return new DeltaFrame<T>
            {
                Type = FrameType.Delta,
                EncoderUuid = uuid,
                Version = version,
                Timestamp = timestamp,
                Patch = patch
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, Frame<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("f", (byte)value.Type);
        writer.WriteString("u", value.EncoderUuid);
        writer.WriteNumber("v", value.Version);
        writer.WriteString("t", value.Timestamp);

        switch (value)
        {
            case KeyFrame<T> kf:
                writer.WritePropertyName("d");
                JsonSerializer.Serialize(writer, kf.Value, options);
                break;
            case DeltaFrame<T> df:
                writer.WritePropertyName("p");
                JsonSerializer.Serialize(writer, df.Patch, _patchType, options);
                break;
        }

        writer.WriteEndObject();
    }
}
