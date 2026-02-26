using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DeltaStreamNet.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class RoundTripBenchmarks
{
    private DeltaStreamEncoder<BenchmarkDto> _encoder = null!;
    private DeltaStreamDecoder<BenchmarkDto> _decoder = null!;

    [IterationSetup]
    public void Reset()
    {
        _encoder = new DeltaStreamEncoder<BenchmarkDto>(BenchmarkState.V1, BenchmarkContext.Default.BenchmarkDto);
        _decoder = new DeltaStreamDecoder<BenchmarkDto>(_encoder.MainFrame);
    }

    [Benchmark(Baseline = true, Description = "All 5 properties changed")]
    public BenchmarkDto? AllChanged()
    {
        var delta = _encoder.EncodeChanges(BenchmarkState.V2AllChanged);
        return _decoder.DecodeFrame(delta);
    }

    [Benchmark(Description = "1 of 5 properties changed")]
    public BenchmarkDto? OneChanged()
    {
        var delta = _encoder.EncodeChanges(BenchmarkState.V2OneChanged);
        return _decoder.DecodeFrame(delta);
    }

    [Benchmark(Description = "No properties changed")]
    public BenchmarkDto? NoneChanged()
    {
        var delta = _encoder.EncodeChanges(BenchmarkState.V2NoneChanged);
        return _decoder.DecodeFrame(delta);
    }
}
