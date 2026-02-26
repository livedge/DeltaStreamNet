namespace DeltaStreamNet.Benchmarks;

[StreamFrame]
public record BenchmarkDto
{
    public required string Name     { get; set; }
    public required int    Score    { get; set; }
    public required double Price    { get; set; }
    public required bool   IsActive { get; set; }
    public required long   Id       { get; set; }
}

[DeltaStreamSerializable(typeof(BenchmarkDto))]
public partial class BenchmarkContext : DeltaStreamContext { }
