using System.Collections.Generic;

namespace DeltaStreamNet.Sample;

[StreamFrame]
public record SampleMarketDto
{
    [StreamKey]
    public MarketType MarketType { get; set; }
    public List<SampleRunnerDto> Runners { get; set; }
}
