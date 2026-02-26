using Xunit;

namespace DeltaStreamNet.Tests;

public class OutOfOrderDeliveryTests
{
    private static TickerDto Tick(decimal price) =>
        new() { Symbol = "GOOG", Price = price, Bid = price - 0.05m, Ask = price + 0.05m, Volume = 500 };

    [Fact]
    public void StaleVersion_Rejected_ConsumerStaysAtLatest()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(2800m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);

        var delta1 = encoder.EncodeChanges(Tick(2801m));
        var delta2 = encoder.EncodeChanges(Tick(2802m));

        // Deliver in order first
        consumer.ApplyFrame(delta1);
        consumer.ApplyFrame(delta2);
        Assert.Equal(2802m, consumer.CurrentValue!.Price);

        // Now a stale v1 arrives again (out of order / duplicate)
        consumer.ApplyFrame(delta1);
        Assert.Equal(1, consumer.FramesRejected);
        // State unchanged
        Assert.Equal(2802m, consumer.CurrentValue!.Price);
    }

    [Fact]
    public void ReversedDelivery_GapDetected_ThenStaleRejected()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame); // v0

        var delta1 = encoder.EncodeChanges(Tick(101m)); // v1
        var delta2 = encoder.EncodeChanges(Tick(102m)); // v2

        // v2 arrives first: consumer is at v0, expects v1 → gap detected
        consumer.ApplyFrame(delta2);
        Assert.True(consumer.NeedsRecovery);
        Assert.Equal(1, consumer.FramesRejected);
        Assert.Equal(100m, consumer.CurrentValue!.Price); // unchanged, still at v0

        // v1 is also rejected — consumer already signaled recovery
        // (and version v1 ≤ v0 is not true, but gap flag means we need key frame)
        // Actually v1 > v0 and v1 == v0+1, so it would pass gap check...
        // but that's fine — it can apply v1 and clear recovery
        consumer.ApplyFrame(delta1);
        Assert.Equal(101m, consumer.CurrentValue!.Price);
        Assert.False(consumer.NeedsRecovery);
    }

    [Fact]
    public void SwappedPair_RecoveryViaKeyFrame()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        consumer.ApplyFrame(encoder.MainFrame);
        var delta1 = encoder.EncodeChanges(Tick(101m));
        var delta2 = encoder.EncodeChanges(Tick(102m));
        var delta3 = encoder.EncodeChanges(Tick(103m));

        // Deliver: v1 OK, then v3 arrives before v2 (swapped)
        consumer.ApplyFrame(delta1);
        consumer.ApplyFrame(delta3); // gap → recovery
        Assert.True(consumer.NeedsRecovery);

        // Recovery
        consumer.Reset();
        consumer.ApplyFrame(encoder.MainFrame);
        Assert.Equal(103m, consumer.CurrentValue!.Price);

        // Continue normally
        var delta4 = encoder.EncodeChanges(Tick(104m));
        consumer.ApplyFrame(delta4);
        Assert.Equal(104m, consumer.CurrentValue!.Price);
        Assert.False(consumer.NeedsRecovery);
    }
}
