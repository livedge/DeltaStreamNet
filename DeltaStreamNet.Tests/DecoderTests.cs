using System;
using Xunit;

namespace DeltaStreamNet.Tests;

public class DecoderTests
{
    [Fact]
    public void DecodeFrame_DeltaFrame_AppliesChangedProperties()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        var delta = encoder.EncodeChanges(new TestDto { Name = "Alice", Score = 200 });
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("Alice", result!.Name);
        Assert.Equal(200, result.Score);
    }

    [Fact]
    public void DecodeFrame_DeltaFrame_PreservesUnchangedProperties()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        // Only Score changes
        var delta = encoder.EncodeChanges(new TestDto { Name = "Alice", Score = 999 });
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("Alice", result!.Name);  // preserved from previous state
        Assert.Equal(999, result.Score);       // updated from patch
    }

    [Fact]
    public void DecodeFrame_KeyFrame_ReplacesFullState()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        var newKeyFrame = new KeyFrame<TestDto>
        {
            Type = FrameType.Key,
            EncoderUuid = encoder.MainFrame.EncoderUuid,
            Version = 5,
            Timestamp = DateTime.UtcNow,
            Value = new TestDto { Name = "Bob", Score = 500 }
        };

        var result = decoder.DecodeFrame(newKeyFrame);

        Assert.Equal("Bob", result!.Name);
        Assert.Equal(500, result.Score);
    }

    [Fact]
    public void DecodeFrame_ThrowsOnUuidMismatch()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        var wrongUuidFrame = new KeyFrame<TestDto>
        {
            Type = FrameType.Key,
            EncoderUuid = Guid.NewGuid(),
            Version = 1,
            Timestamp = DateTime.UtcNow,
            Value = new TestDto { Name = "Bob", Score = 200 }
        };

        Assert.Throws<ArgumentException>(() => decoder.DecodeFrame(wrongUuidFrame));
    }

    [Fact]
    public void DecodeFrame_ThrowsOnOldVersion()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);
        var decoder = new DeltaStreamDecoder<TestDto>(encoder.MainFrame);

        // Advance decoder to version 1
        decoder.DecodeFrame(encoder.EncodeChanges(new TestDto { Name = "Alice", Score = 200 }));

        var staleFrame = new KeyFrame<TestDto>
        {
            Type = FrameType.Key,
            EncoderUuid = encoder.MainFrame.EncoderUuid,
            Version = 0,
            Timestamp = DateTime.UtcNow,
            Value = new TestDto { Name = "Alice", Score = 100 }
        };

        Assert.Throws<ArgumentException>(() => decoder.DecodeFrame(staleFrame));
    }
}
