using System;
using System.Collections.Generic;

namespace DeltaStreamNet.Sample;

[StreamFrame]
public record SampleEventDto
{
    public required string Competitor1 { get; set; }
    public required string Competitor2 { get; set; }
    public required string League { get; set; }
    public required DateTime Timestamp { get; set; }
    public required List<SampleMarketDto> Markets { get; set; }
}
