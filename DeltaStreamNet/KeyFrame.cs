using System.Text.Json.Serialization;

namespace DeltaStreamNet;

public record KeyFrame<T> : Frame<T>
{
    [JsonPropertyName("d")]
    public required T Value { get; init; }
}
