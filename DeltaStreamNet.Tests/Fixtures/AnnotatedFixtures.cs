using System.Text.Json.Serialization;
using ProtoBuf;

namespace DeltaStreamNet.Tests;

[StreamFrame(PropagateAttributes = true)]
public record AnnotatedDto
{
    [JsonPropertyName("player_name")]
    public required string Name  { get; set; }

    [JsonPropertyName("player_score")]
    public required int    Score { get; set; }
}

[DeltaStreamSerializable(typeof(AnnotatedDto))]
public partial class AnnotatedContext : DeltaStreamContext { }

// --- Protobuf attribute propagation -----------------------------------------

[ProtoContract]
[StreamFrame(PropagateAttributes = true)]
public record ProtobufAnnotatedDto
{
    [ProtoMember(1)]
    public required string Name  { get; set; }

    [ProtoMember(2)]
    public required int    Score { get; set; }
}

[DeltaStreamSerializable(typeof(ProtobufAnnotatedDto))]
public partial class ProtobufAnnotatedContext : DeltaStreamContext { }
