using Xunit;

namespace DeltaStreamNet.Tests;

public class DroppedMessageTests
{
    private static TickerDto Tick(decimal price) =>
        new() { Symbol = "MSFT", Price = price, Bid = price - 0.02m, Ask = price + 0.02m, Volume = 2000 };

    [Fact]
    public void DroppedDelta_CausesVersionGap_ConsumerSignalsRecovery()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(300m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);

        var delta1 = encoder.EncodeChanges(Tick(301m)); // v1
        var _      = encoder.EncodeChanges(Tick(302m)); // v2 — will be "dropped"
        var delta3 = encoder.EncodeChanges(Tick(303m)); // v3

        // Deliver v1 then skip v2 and try v3
        consumer.ApplyFrame(delta1);
        Assert.Equal(301m, consumer.CurrentValue!.Price);

        // v3 arrives but decoder is at v1 — v3 > v1+1 → recovery needed
        consumer.ApplyFrame(delta3);
        Assert.True(consumer.NeedsRecovery);
        Assert.Equal(1, consumer.FramesRejected);
    }

    [Fact]
    public void DroppedDelta_RecoveredWithKeyFrame_ConsumerResumes()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(300m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);
        consumer.ApplyFrame(encoder.EncodeChanges(Tick(301m)));

        var _ = encoder.EncodeChanges(Tick(302m)); // dropped
        var delta3 = encoder.EncodeChanges(Tick(303m));

        // Consumer can't apply delta3 (gap)
        consumer.ApplyFrame(delta3);
        Assert.True(consumer.NeedsRecovery);

        // Recovery: producer sends fresh key frame
        consumer.Reset();
        consumer.ApplyFrame(encoder.MainFrame); // version 3 with full state

        Assert.Equal(303m, consumer.CurrentValue!.Price);
        Assert.False(consumer.NeedsRecovery);

        // Consumer can continue receiving deltas after recovery
        var delta4 = encoder.EncodeChanges(Tick(304m));
        consumer.ApplyFrame(delta4);
        Assert.Equal(304m, consumer.CurrentValue!.Price);
    }

    [Fact]
    public void MultipleLostMessages_SingleKeyFrameRecovery()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(200m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);

        // Produce 10 deltas, consumer only receives #1 and #10
        var deltas = new DeltaFrame<TickerDto>[10];
        for (var i = 0; i < 10; i++)
            deltas[i] = encoder.EncodeChanges(Tick(200m + i + 1));

        consumer.ApplyFrame(deltas[0]); // v1 OK
        Assert.Equal(201m, consumer.CurrentValue!.Price);

        consumer.ApplyFrame(deltas[9]); // v10 — gap from v1
        Assert.True(consumer.NeedsRecovery);

        // Single key frame recovery restores to v10 state
        consumer.Reset();
        consumer.ApplyFrame(encoder.MainFrame);
        Assert.Equal(210m, consumer.CurrentValue!.Price);
        Assert.False(consumer.NeedsRecovery);
    }

    [Fact]
    public void ConsumerWithoutKeyFrame_RejectsDelta()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        // Consumer receives a delta without ever getting a key frame
        var delta = encoder.EncodeChanges(Tick(101m));
        consumer.ApplyFrame(delta);

        Assert.True(consumer.NeedsRecovery);
        Assert.Equal(1, consumer.FramesRejected);
        Assert.Null(consumer.CurrentValue);
    }
}
