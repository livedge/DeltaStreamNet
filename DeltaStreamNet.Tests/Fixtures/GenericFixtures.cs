using System.Text.Json.Serialization;

namespace DeltaStreamNet.Tests;

[StreamFrame]
public record InnerDto
{
    [JsonPropertyName("tag")]
    public required string Tag { get; set; }

    [JsonPropertyName("value")]
    public required int Value { get; set; }
}

[StreamFrame(PropagateAttributes = true)]
public record WrapperDto<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }
}

[DeltaStreamSerializable(typeof(InnerDto))]
[DeltaStreamSerializable(typeof(WrapperDto<InnerDto>))]
public partial class GenericContext : DeltaStreamContext;
