using System.Text.Json.Serialization;

namespace DeltaStreamNet;

public record Frame<T>
{
    [JsonPropertyName("f")]
    public required FrameType Type { get; init; }

    [JsonPropertyName("u")]
    public required Guid EncoderUuid { get; init; }

    [JsonPropertyName("v")]
    public required ulong Version { get; init; }

    [JsonPropertyName("t")]
    public required DateTime Timestamp { get; init; }
}
