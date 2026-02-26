using Xunit;

namespace DeltaStreamNet.Tests;

public class NetworkPartitionTests
{
    private static TickerDto Tick(decimal price) =>
        new() { Symbol = "NVDA", Price = price, Bid = price - 0.03m, Ask = price + 0.03m, Volume = 800 };

    [Fact]
    public void Partition_ConsumerMissesMessages_RecoveriesWithSnapshot()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(800m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        // Phase 1: normal operation
        consumer.ApplyFrame(encoder.MainFrame);
        consumer.ApplyFrame(encoder.EncodeChanges(Tick(801m)));
        consumer.ApplyFrame(encoder.EncodeChanges(Tick(802m)));
        Assert.Equal(802m, consumer.CurrentValue!.Price);

        // Phase 2: network partition — producer keeps going, consumer gets nothing
        for (var i = 0; i < 20; i++)
            encoder.EncodeChanges(Tick(803m + i));

        // Phase 3: partition heals — consumer receives a delta with huge version gap
        var lateDelta = encoder.EncodeChanges(Tick(900m));
        consumer.ApplyFrame(lateDelta);
        Assert.True(consumer.NeedsRecovery);

        // Phase 4: consumer requests snapshot (key frame)
        consumer.Reset();
        consumer.ApplyFrame(encoder.MainFrame);
        Assert.Equal(900m, consumer.CurrentValue!.Price);
        Assert.False(consumer.NeedsRecovery);

        // Phase 5: normal operation resumes
        consumer.ApplyFrame(encoder.EncodeChanges(Tick(901m)));
        Assert.Equal(901m, consumer.CurrentValue!.Price);
    }

    [Fact]
    public void RepeatedPartitions_EachRecoveredIndependently()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();
        consumer.ApplyFrame(encoder.MainFrame);

        var totalRecoveries = 0;

        for (var partition = 0; partition < 3; partition++)
        {
            // Normal: 5 deltas applied
            for (var i = 0; i < 5; i++)
                consumer.ApplyFrame(encoder.EncodeChanges(
                    Tick(100m + (partition + 1) * 100m + i)));

            // Partition: 5 deltas lost
            for (var i = 0; i < 5; i++)
                encoder.EncodeChanges(Tick(100m + (partition + 1) * 100m + 10 + i));

            // Gap delta arrives
            var gapDelta = encoder.EncodeChanges(Tick(100m + (partition + 1) * 1000m));
            consumer.ApplyFrame(gapDelta);
            Assert.True(consumer.NeedsRecovery);

            // Recovery
            consumer.Reset();
            consumer.ApplyFrame(encoder.MainFrame);
            totalRecoveries++;
            Assert.False(consumer.NeedsRecovery);
        }

        Assert.Equal(3, totalRecoveries);
        Assert.Equal(encoder.MainFrame.Value.Price, consumer.CurrentValue!.Price);
    }
}
