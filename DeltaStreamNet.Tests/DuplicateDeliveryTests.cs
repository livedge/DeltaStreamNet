using System;
using Xunit;

namespace DeltaStreamNet.Tests;

public class DuplicateDeliveryTests
{
    private static TickerDto Tick(decimal price) =>
        new() { Symbol = "TSLA", Price = price, Bid = price - 0.10m, Ask = price + 0.10m, Volume = 3000 };

    [Fact]
    public void ExactDuplicate_Rejected_StateUnchanged()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(250m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);
        var delta1 = encoder.EncodeChanges(Tick(251m));

        consumer.ApplyFrame(delta1);
        Assert.Equal(251m, consumer.CurrentValue!.Price);

        // Kafka at-least-once: same message re-delivered
        consumer.ApplyFrame(delta1);
        Assert.Equal(1, consumer.FramesRejected);
        Assert.Equal(251m, consumer.CurrentValue!.Price); // unchanged
    }

    [Fact]
    public void KeyFrameDuplicate_Rejected_StateUnchanged()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(250m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);
        var delta1 = encoder.EncodeChanges(Tick(260m));
        consumer.ApplyFrame(delta1);

        // Original key frame (v0) arrives again
        var staleKf = new KeyFrame<TickerDto>
        {
            Type = FrameType.Key,
            EncoderUuid = encoder.MainFrame.EncoderUuid,
            Version = 0,
            Timestamp = DateTime.UtcNow,
            Value = Tick(250m)
        };
        consumer.ApplyFrame(staleKf);
        Assert.Equal(1, consumer.FramesRejected);
        Assert.Equal(260m, consumer.CurrentValue!.Price); // not reverted
    }

    [Fact]
    public void TripleDuplicate_AllRejected_AppliedCountUnchanged()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);
        var delta = encoder.EncodeChanges(Tick(101m));
        consumer.ApplyFrame(delta);

        var appliedBefore = consumer.FramesApplied;

        consumer.ApplyFrame(delta);
        consumer.ApplyFrame(delta);
        consumer.ApplyFrame(delta);

        Assert.Equal(appliedBefore, consumer.FramesApplied);
        Assert.Equal(3, consumer.FramesRejected);
    }
}
