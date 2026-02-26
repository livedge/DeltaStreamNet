using System.Text.Json.Serialization;
using ProtoBuf;

namespace DeltaStreamNet.Benchmarks;

[ProtoContract]
[StreamFrame(PropagateAttributes = true, MinifyJson = true)]
public record PayloadDto
{
    [JsonPropertyName("name")]
    [ProtoMember(1)]
    public required string Name { get; set; }

    [JsonPropertyName("score")]
    [ProtoMember(2)]
    public required int Score { get; set; }

    [JsonPropertyName("price")]
    [ProtoMember(3)]
    public required double Price { get; set; }

    [JsonPropertyName("is_active")]
    [ProtoMember(4)]
    public required bool IsActive { get; set; }

    [JsonPropertyName("id")]
    [ProtoMember(5)]
    public required long Id { get; set; }
}

[DeltaStreamSerializable(typeof(PayloadDto))]
public partial class PayloadContext : DeltaStreamContext { }
