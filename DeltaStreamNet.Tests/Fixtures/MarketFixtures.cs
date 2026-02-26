using System.Collections.Generic;

namespace DeltaStreamNet.Tests;

[StreamFrame]
public record MarketItemDto
{
    [StreamKey]
    public required string Id { get; set; }
    public required decimal Price { get; set; }
    public required int Volume { get; set; }
}

[StreamFrame]
public record MarketBoardDto
{
    public required string Name { get; set; }
    public required List<MarketItemDto> Items { get; set; }
}

[DeltaStreamSerializable(typeof(MarketBoardDto))]
public partial class MarketBoardContext : DeltaStreamContext { }
