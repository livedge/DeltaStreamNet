using System.Text.Json.Serialization;

namespace DeltaStreamNet;

public record DeltaFrame<T> : Frame<T>
{
    [JsonPropertyName("p")]
    public required IDeltaPatch<T> Patch { get; init; }
}
