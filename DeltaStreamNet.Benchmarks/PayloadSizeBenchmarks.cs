using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ProtoBuf.Meta;

namespace DeltaStreamNet.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class PayloadSizeBenchmarks
{
    private PayloadDtoKeyFrame _keyFrame = null!;
    private PayloadDtoDeltaFrame _deltaAllChanged = null!;
    private PayloadDtoDeltaFrame _deltaOneChanged = null!;
    private PayloadDtoDeltaFrame _deltaNoneChanged = null!;
    private RuntimeTypeModel _protoModel = null!;
    private JsonSerializerOptions _jsonOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _keyFrame = PayloadDtoKeyFrame.From(PayloadHelpers.V1);
        var kfPrev = PayloadDtoKeyFrame.From(PayloadHelpers.V1);
        _deltaAllChanged = PayloadDtoDeltaFrame.Create(kfPrev, PayloadDtoKeyFrame.From(PayloadHelpers.V2AllChanged));
        _deltaOneChanged = PayloadDtoDeltaFrame.Create(kfPrev, PayloadDtoKeyFrame.From(PayloadHelpers.V2OneChanged));
        _deltaNoneChanged = PayloadDtoDeltaFrame.Create(kfPrev, PayloadDtoKeyFrame.From(PayloadHelpers.V2NoneChanged));
        _protoModel = PayloadHelpers.BuildProtoModel();
        _jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    }

    // --- JSON ----------------------------------------------------------------

    [Benchmark(Baseline = true, Description = "JSON Full DTO")]
    public byte[] Json_FullDto() =>
        JsonSerializer.SerializeToUtf8Bytes(PayloadHelpers.V1, _jsonOptions);

    [Benchmark(Description = "JSON KeyFrame")]
    public byte[] Json_KeyFrame() =>
        JsonSerializer.SerializeToUtf8Bytes(_keyFrame, _jsonOptions);

    [Benchmark(Description = "JSON Delta (all 5 changed)")]
    public byte[] Json_Delta_AllChanged() =>
        JsonSerializer.SerializeToUtf8Bytes(_deltaAllChanged, _jsonOptions);

    [Benchmark(Description = "JSON Delta (1 of 5 changed)")]
    public byte[] Json_Delta_OneChanged() =>
        JsonSerializer.SerializeToUtf8Bytes(_deltaOneChanged, _jsonOptions);

    [Benchmark(Description = "JSON Delta (none changed)")]
    public byte[] Json_Delta_NoneChanged() =>
        JsonSerializer.SerializeToUtf8Bytes(_deltaNoneChanged, _jsonOptions);

    // --- Protobuf ------------------------------------------------------------

    [Benchmark(Description = "Protobuf KeyFrame")]
    public byte[] Proto_KeyFrame()
    {
        using var ms = new MemoryStream();
        _protoModel.Serialize(ms, _keyFrame);
        return ms.ToArray();
    }

    [Benchmark(Description = "Protobuf Delta (all 5 changed)")]
    public byte[] Proto_Delta_AllChanged()
    {
        using var ms = new MemoryStream();
        _protoModel.Serialize(ms, _deltaAllChanged);
        return ms.ToArray();
    }

    [Benchmark(Description = "Protobuf Delta (1 of 5 changed)")]
    public byte[] Proto_Delta_OneChanged()
    {
        using var ms = new MemoryStream();
        _protoModel.Serialize(ms, _deltaOneChanged);
        return ms.ToArray();
    }

    [Benchmark(Description = "Protobuf Delta (none changed)")]
    public byte[] Proto_Delta_NoneChanged()
    {
        using var ms = new MemoryStream();
        _protoModel.Serialize(ms, _deltaNoneChanged);
        return ms.ToArray();
    }
}
