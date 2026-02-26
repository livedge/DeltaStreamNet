using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DeltaStreamNet.Tests;

public class BurstAndThroughputTests
{
    private static TickerDto Tick(decimal price, long vol) =>
        new() { Symbol = "BTC", Price = price, Bid = price - 1m, Ask = price + 1m, Volume = vol };

    [Fact]
    public void HighFrequencyBurst_1000Deltas_AllApplied()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(50000m, 0), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();
        consumer.ApplyFrame(encoder.MainFrame);

        for (var i = 1; i <= 1000; i++)
        {
            var delta = encoder.EncodeChanges(Tick(50000m + i, i));
            consumer.ApplyFrame(delta);
        }

        Assert.Equal(51000m, consumer.CurrentValue!.Price);
        Assert.Equal(1000L, consumer.CurrentValue!.Volume);
        Assert.Equal(1001, consumer.FramesApplied); // 1 key + 1000 delta
        Assert.Equal(0, consumer.FramesRejected);
    }

    [Fact]
    public void Burst_WithPeriodicDrops_RecoveryCountTracked()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m, 0), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();
        consumer.ApplyFrame(encoder.MainFrame);

        var recoveryCount = 0;
        var deltas = new List<DeltaFrame<TickerDto>>();

        // Produce 50 deltas
        for (var i = 1; i <= 50; i++)
            deltas.Add(encoder.EncodeChanges(Tick(100m + i, i)));

        // Deliver with every 10th message dropped
        for (var i = 0; i < deltas.Count; i++)
        {
            if ((i + 1) % 10 == 0)
                continue; // drop every 10th

            consumer.ApplyFrame(deltas[i]);

            if (consumer.NeedsRecovery)
            {
                recoveryCount++;
                consumer.Reset();
                consumer.ApplyFrame(encoder.MainFrame);
            }
        }

        // Should have needed recovery multiple times
        Assert.True(recoveryCount > 0);
        // After final recovery, consumer should be at latest encoder state
        Assert.Equal(150m, consumer.CurrentValue!.Price);
    }

    [Fact]
    public void CatchUp_ConsumerProcessesBacklog_ReachesLatest()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m, 0), TickerContext.Default.TickerDto);
        var transport = new SimulatedTransport();

        // Producer publishes 20 messages while consumer is "offline"
        transport.Publish(WireCodec.EncodeKeyFrame(encoder.MainFrame));
        for (var i = 1; i <= 20; i++)
        {
            var delta = encoder.EncodeChanges(Tick(100m + i, i));
            transport.Publish(WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch));
        }

        // Consumer comes online and processes the entire backlog
        var consumer = new StreamConsumer<TickerDto>();
        var messages = transport.DrainAll();

        foreach (var msg in messages)
        {
            var frame = WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg);
            consumer.ApplyFrame(frame);
        }

        Assert.Equal(120m, consumer.CurrentValue!.Price);
        Assert.Equal(21, consumer.FramesApplied);
    }
}
