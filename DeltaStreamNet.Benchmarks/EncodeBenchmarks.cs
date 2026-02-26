using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DeltaStreamNet.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class EncodeBenchmarks
{
    private DeltaStreamEncoder<BenchmarkDto> _encoder = null!;

    [IterationSetup]
    public void ResetEncoder() =>
        _encoder = new DeltaStreamEncoder<BenchmarkDto>(BenchmarkState.V1, BenchmarkContext.Default.BenchmarkDto);

    [Benchmark(Baseline = true, Description = "All 5 properties changed")]
    public DeltaFrame<BenchmarkDto> AllChanged() =>
        _encoder.EncodeChanges(BenchmarkState.V2AllChanged);

    [Benchmark(Description = "1 of 5 properties changed")]
    public DeltaFrame<BenchmarkDto> OneChanged() =>
        _encoder.EncodeChanges(BenchmarkState.V2OneChanged);

    [Benchmark(Description = "No properties changed")]
    public DeltaFrame<BenchmarkDto> NoneChanged() =>
        _encoder.EncodeChanges(BenchmarkState.V2NoneChanged);
}
