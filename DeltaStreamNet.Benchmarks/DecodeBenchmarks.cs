using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DeltaStreamNet.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class DecodeBenchmarks
{
    private DeltaStreamDecoder<BenchmarkDto> _decoder     = null!;
    private DeltaFrame<BenchmarkDto>         _allChanged  = null!;
    private DeltaFrame<BenchmarkDto>         _oneChanged  = null!;
    private DeltaFrame<BenchmarkDto>         _noneChanged = null!;

    [GlobalSetup]
    public void BuildFrames()
    {
        var enc = new DeltaStreamEncoder<BenchmarkDto>(BenchmarkState.V1, BenchmarkContext.Default.BenchmarkDto);
        _allChanged  = enc.EncodeChanges(BenchmarkState.V2AllChanged);

        enc = new DeltaStreamEncoder<BenchmarkDto>(BenchmarkState.V1, BenchmarkContext.Default.BenchmarkDto);
        _oneChanged  = enc.EncodeChanges(BenchmarkState.V2OneChanged);

        enc = new DeltaStreamEncoder<BenchmarkDto>(BenchmarkState.V1, BenchmarkContext.Default.BenchmarkDto);
        _noneChanged = enc.EncodeChanges(BenchmarkState.V2NoneChanged);
    }

    [IterationSetup]
    public void ResetDecoder()
    {
        var enc = new DeltaStreamEncoder<BenchmarkDto>(BenchmarkState.V1, BenchmarkContext.Default.BenchmarkDto);
        _decoder = new DeltaStreamDecoder<BenchmarkDto>(enc.MainFrame);
    }

    [Benchmark(Baseline = true, Description = "All 5 properties changed")]
    public BenchmarkDto? AllChanged() =>
        _decoder.DecodeFrame(_allChanged);

    [Benchmark(Description = "1 of 5 properties changed")]
    public BenchmarkDto? OneChanged() =>
        _decoder.DecodeFrame(_oneChanged);

    [Benchmark(Description = "No properties changed")]
    public BenchmarkDto? NoneChanged() =>
        _decoder.DecodeFrame(_noneChanged);
}
