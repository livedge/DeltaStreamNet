using System.IO;
using ProtoBuf;
using Xunit;

namespace DeltaStreamNet.Tests;

public class ProtobufFrameRoundTripTests
{
    static ProtobufFrameRoundTripTests()
    {
        // Ensure the generated context is initialized so Register calls happen
        _ = ProtobufAnnotatedContext.Default;
    }

    [Fact]
    public void KeyFrame_RoundTrips_ViaProtobuf()
    {
        var initial = new ProtobufAnnotatedDto { Name = "Alice", Score = 100 };
        var encoder = new DeltaStreamEncoder<ProtobufAnnotatedDto>(initial, ProtobufAnnotatedContext.Default.ProtobufAnnotatedDto);
        Frame<ProtobufAnnotatedDto> frame = encoder.MainFrame;

        using var ms = new MemoryStream();
        Serializer.Serialize(ms, frame);

        ms.Position = 0;
        var deserialized = Serializer.Deserialize<Frame<ProtobufAnnotatedDto>>(ms);

        Assert.IsType<KeyFrame<ProtobufAnnotatedDto>>(deserialized);
        var keyFrame = (KeyFrame<ProtobufAnnotatedDto>)deserialized;
        Assert.Equal("Alice", keyFrame.Value.Name);
        Assert.Equal(100, keyFrame.Value.Score);
    }

    [Fact]
    public void DeltaFrame_RoundTrips_ViaProtobuf()
    {
        var initial = new ProtobufAnnotatedDto { Name = "Bob", Score = 42 };
        var encoder = new DeltaStreamEncoder<ProtobufAnnotatedDto>(initial, ProtobufAnnotatedContext.Default.ProtobufAnnotatedDto);
        var updated = new ProtobufAnnotatedDto { Name = "Bob", Score = 99 };
        Frame<ProtobufAnnotatedDto> delta = encoder.EncodeChanges(updated);

        using var ms = new MemoryStream();
        Serializer.Serialize(ms, delta);

        ms.Position = 0;
        var deserialized = Serializer.Deserialize<Frame<ProtobufAnnotatedDto>>(ms);

        Assert.IsType<DeltaFrame<ProtobufAnnotatedDto>>(deserialized);
        var deltaFrame = (DeltaFrame<ProtobufAnnotatedDto>)deserialized;

        var result = deltaFrame.Patch.ApplyPatch(initial);
        Assert.Equal("Bob", result.Name);
        Assert.Equal(99, result.Score);
    }

    [Fact]
    public void FullRoundTrip_Encode_Serialize_Deserialize_Decode()
    {
        var initial = new ProtobufAnnotatedDto { Name = "Charlie", Score = 10 };
        var encoder = new DeltaStreamEncoder<ProtobufAnnotatedDto>(initial, ProtobufAnnotatedContext.Default.ProtobufAnnotatedDto);

        // Serialize KeyFrame
        using var keyMs = new MemoryStream();
        Serializer.Serialize<Frame<ProtobufAnnotatedDto>>(keyMs, encoder.MainFrame);
        keyMs.Position = 0;
        var deserializedKey = Serializer.Deserialize<Frame<ProtobufAnnotatedDto>>(keyMs);
        var decoder = new DeltaStreamDecoder<ProtobufAnnotatedDto>((KeyFrame<ProtobufAnnotatedDto>)deserializedKey);

        // Encode a change
        var updated = new ProtobufAnnotatedDto { Name = "Charlie", Score = 50 };
        var delta = encoder.EncodeChanges(updated);

        // Serialize DeltaFrame
        using var deltaMs = new MemoryStream();
        Serializer.Serialize<Frame<ProtobufAnnotatedDto>>(deltaMs, delta);
        deltaMs.Position = 0;
        var deserializedDelta = Serializer.Deserialize<Frame<ProtobufAnnotatedDto>>(deltaMs);

        // Decode
        var result = decoder.DecodeFrame(deserializedDelta);

        Assert.NotNull(result);
        Assert.Equal("Charlie", result!.Name);
        Assert.Equal(50, result.Score);
    }
}
