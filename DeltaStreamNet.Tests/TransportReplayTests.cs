using System.Linq;
using Xunit;

namespace DeltaStreamNet.Tests;

public class TransportReplayTests
{
    private static TickerDto Tick(decimal price) =>
        new() { Symbol = "ETH", Price = price, Bid = price - 0.5m, Ask = price + 0.5m, Volume = 100 };

    [Fact]
    public void Replay_AfterReset_ConsumerResynchronizes()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(3000m), TickerContext.Default.TickerDto);
        var transport = new SimulatedTransport();

        // Publish key frame + 5 deltas
        transport.Publish(WireCodec.EncodeKeyFrame(encoder.MainFrame));
        for (var i = 1; i <= 5; i++)
        {
            var delta = encoder.EncodeChanges(Tick(3000m + i));
            transport.Publish(WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch));
        }

        // Consumer processes first 3 messages (key + delta1 + delta2)
        var consumer = new StreamConsumer<TickerDto>();
        var batch1 = transport.DrainAll().Take(3).ToList();
        foreach (var msg in batch1)
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        Assert.Equal(3002m, consumer.CurrentValue!.Price);

        // Consumer crashes and restarts — needs full replay from beginning
        consumer.Reset();
        transport.Replay(fromIndex: 0, count: 6); // replay all 6 messages

        var replayed = transport.DrainAll();
        foreach (var msg in replayed)
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        Assert.Equal(3005m, consumer.CurrentValue!.Price);
        Assert.False(consumer.NeedsRecovery);
    }

    [Fact]
    public void Replay_PartialFromOffset_ConsumerCatchesUp()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(1000m), TickerContext.Default.TickerDto);
        var transport = new SimulatedTransport();

        // Publish to log only (no auto-delivery) — simulates Kafka topic
        transport.PublishSilent(WireCodec.EncodeKeyFrame(encoder.MainFrame)); // [0]
        for (var i = 1; i <= 10; i++)
        {
            var delta = encoder.EncodeChanges(Tick(1000m + i));
            transport.PublishSilent(WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch));
        }

        // Consumer seeks to offset 0, reads first 6 entries (key + deltas 1-5)
        var consumer = new StreamConsumer<TickerDto>();
        transport.Replay(0, 6);
        foreach (var msg in transport.DrainAll())
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        Assert.Equal(1005m, consumer.CurrentValue!.Price);

        // Consumer continues from offset 6, reads remaining 5 deltas
        transport.Replay(6, 5);
        foreach (var msg in transport.DrainAll())
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        Assert.Equal(1010m, consumer.CurrentValue!.Price);
    }

    [Fact]
    public void Redeliver_SingleDuplicate_Idempotent()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(500m), TickerContext.Default.TickerDto);
        var transport = new SimulatedTransport();

        // Build log without auto-delivery
        transport.PublishSilent(WireCodec.EncodeKeyFrame(encoder.MainFrame)); // [0]
        var delta1 = encoder.EncodeChanges(Tick(501m));
        transport.PublishSilent(WireCodec.EncodeDeltaFrame(delta1, (TickerDtoDeltaFrame)delta1.Patch)); // [1]
        var delta2 = encoder.EncodeChanges(Tick(502m));
        transport.PublishSilent(WireCodec.EncodeDeltaFrame(delta2, (TickerDtoDeltaFrame)delta2.Patch)); // [2]

        var consumer = new StreamConsumer<TickerDto>();

        // Deliver: key, delta1
        transport.DeliverInOrder(0, 1);
        foreach (var msg in transport.DrainAll())
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        // Redeliver delta1 (duplicate) then delta2
        transport.RedeliverAt(1);
        transport.DeliverInOrder(2);
        foreach (var msg in transport.DrainAll())
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        Assert.Equal(502m, consumer.CurrentValue!.Price);
        Assert.Equal(1, consumer.FramesRejected); // the duplicate
    }
}
