namespace DeltaStreamNet.Sample;

[StreamFrame]
public record SampleRunnerDto
{
    [StreamKey]
    public required string RunnerName { get; set; }
    public required decimal Price { get; set; }
}
