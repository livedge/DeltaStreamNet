using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class RoundTripTests
{
    [Fact]
    public void MultipleDeltas_DecodedInOrder_ProduceCorrectState()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 0 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        var delta1 = encoder.EncodeChanges(new TestDto { Name = "Alice", Score = 10 });
        var delta2 = encoder.EncodeChanges(new TestDto { Name = "Bob",   Score = 10 });
        var delta3 = encoder.EncodeChanges(new TestDto { Name = "Bob",   Score = 20 });

        Assert.Equal(10,    decoder.DecodeFrame(delta1)!.Score);
        Assert.Equal("Bob", decoder.DecodeFrame(delta2)!.Name);
        Assert.Equal(20,    decoder.DecodeFrame(delta3)!.Score);
    }

    [Fact]
    public void Patch_AppliedDirectlyToInitialState_ProducesFinalState()
    {
        var initial = new TestDto { Name = "Alice", Score = 100 };
        var updated = new TestDto { Name = "Bob",   Score = 100 };

        var encoder = new DeltaStreamEncoder<TestDto>(initial, TestContext.Default.TestDto);
        var delta = encoder.EncodeChanges(updated);

        var result = delta.Patch.ApplyPatch(initial);

        Assert.Equal(updated, result);
    }

    [Fact]
    public void JsonSerialize_DeltaFrame_DeserializesWithNoOptions()
    {
        var initial = new TestDto { Name = "Alice", Score = 100 };
        var updated = new TestDto { Name = "Bob", Score = 100 };

        var encoder = new DeltaStreamEncoder<TestDto>(initial, TestContext.Default.TestDto);
        var delta = encoder.EncodeChanges(updated);

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.SerializeToUtf8Bytes<Frame<TestDto>>(delta, options);
        var frame = JsonSerializer.Deserialize<Frame<TestDto>>(json, options);

        Assert.NotNull(frame);
        Assert.IsType<DeltaFrame<TestDto>>(frame);
        var df = (DeltaFrame<TestDto>)frame!;
        Assert.Equal(FrameType.Delta, df.Type);
        Assert.Equal(delta.EncoderUuid, df.EncoderUuid);
        Assert.Equal(delta.Version, df.Version);

        var result = df.Patch.ApplyPatch(initial);
        Assert.Equal(updated, result);
    }

    [Fact]
    public void JsonSerialize_KeyFrame_DeserializesWithNoOptions()
    {
        var initial = new TestDto { Name = "Alice", Score = 100 };

        var encoder = new DeltaStreamEncoder<TestDto>(initial, TestContext.Default.TestDto);

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.SerializeToUtf8Bytes<Frame<TestDto>>(encoder.MainFrame, options);
        var frame = JsonSerializer.Deserialize<Frame<TestDto>>(json, options);

        Assert.NotNull(frame);
        Assert.IsType<KeyFrame<TestDto>>(frame);
        var kf = (KeyFrame<TestDto>)frame!;
        Assert.Equal(FrameType.Key, kf.Type);
        Assert.Equal(initial, kf.Value);
    }

    [Fact]
    public void EncoderAndDecoder_AfterManyChanges_StayInSync()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Start", Score = 0 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        for (var i = 1; i <= 10; i++)
        {
            var next = new TestDto { Name = $"v{i}", Score = i * 10 };
            var delta = encoder.EncodeChanges(next);
            var decoded = decoder.DecodeFrame(delta);

            Assert.Equal(next, decoded);
            Assert.Equal((ulong)i, delta.Version);
        }
    }
}
