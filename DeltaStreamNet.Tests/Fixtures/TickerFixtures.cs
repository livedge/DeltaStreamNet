using System.Text.Json.Serialization;

namespace DeltaStreamNet.Tests;

[StreamFrame(MinifyJson = true, PropagateAttributes = true)]
public record TickerDto
{
    [JsonPropertyName("sym")]
    public required string Symbol { get; set; }

    [JsonPropertyName("px")]
    public required decimal Price { get; set; }

    [JsonPropertyName("bid")]
    public required decimal Bid { get; set; }

    [JsonPropertyName("ask")]
    public required decimal Ask { get; set; }

    [JsonPropertyName("vol")]
    public required long Volume { get; set; }
}

[DeltaStreamSerializable(typeof(TickerDto))]
public partial class TickerContext : DeltaStreamContext { }
