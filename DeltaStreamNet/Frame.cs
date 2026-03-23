using System.Text.Json.Serialization;

namespace DeltaStreamNet;

[JsonConverter(typeof(FrameJsonConverterFactory))]
public record Frame<T>
{
    [JsonPropertyName("f")]
    public required FrameType Type { get; init; }

    [JsonPropertyName("u")]
    [JsonConverter(typeof(Base64GuidConverter))]
    public required Guid EncoderUuid { get; init; }

    [JsonPropertyName("v")]
    public required ulong Version { get; init; }

    [JsonPropertyName("t")]
    [JsonConverter(typeof(UnixMillisDateTimeConverter))]
    public required DateTime Timestamp { get; init; }
}
