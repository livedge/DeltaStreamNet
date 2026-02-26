using System.Linq;
using Xunit;

namespace DeltaStreamNet.Tests;

public class HappyPathTransportTests
{
    private static TickerDto Tick(decimal price, long vol = 1000) =>
        new() { Symbol = "AAPL", Price = price, Bid = price - 0.01m, Ask = price + 0.01m, Volume = vol };

    [Fact]
    public void InOrder_AllDeltasApplied_ConsumerMatchesProducer()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(150m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        // Initial key frame
        consumer.ApplyFrame(encoder.MainFrame);
        Assert.Equal(150m, consumer.CurrentValue!.Price);

        // 10 rapid price updates
        for (var i = 1; i <= 10; i++)
        {
            var delta = encoder.EncodeChanges(Tick(150m + i));
            consumer.ApplyFrame(delta);
        }

        Assert.Equal(160m, consumer.CurrentValue!.Price);
        Assert.Equal(11, consumer.FramesApplied);
        Assert.Equal(0, consumer.FramesRejected);
        Assert.False(consumer.NeedsRecovery);
    }

    [Fact]
    public void InOrder_ThroughChannel_AllFramesDelivered()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var transport = new SimulatedTransport();
        var consumer = new StreamConsumer<TickerDto>();

        // Producer publishes key frame + 5 deltas through wire codec
        transport.Publish(WireCodec.EncodeKeyFrame(encoder.MainFrame));
        for (var i = 1; i <= 5; i++)
        {
            var delta = encoder.EncodeChanges(Tick(100m + i));
            transport.Publish(WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch));
        }

        // Consumer drains all and processes
        foreach (var msg in transport.DrainAll())
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(msg));

        Assert.Equal(105m, consumer.CurrentValue!.Price);
        Assert.Equal(6, consumer.FramesApplied);
    }

    [Fact]
    public void JsonWireRoundTrip_KeyFramePreservesData()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(150.50m, 5000), TickerContext.Default.TickerDto);

        var wire = WireCodec.EncodeKeyFrame(encoder.MainFrame);
        var decoded = WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(wire);

        Assert.IsType<KeyFrame<TickerDto>>(decoded);
        var kf = (KeyFrame<TickerDto>)decoded;
        Assert.Equal(encoder.MainFrame.EncoderUuid, kf.EncoderUuid);
        Assert.Equal(0UL, kf.Version);
        Assert.Equal("AAPL", kf.Value.Symbol);
        Assert.Equal(150.50m, kf.Value.Price);
        Assert.Equal(5000, kf.Value.Volume);
    }

    [Fact]
    public void JsonWireRoundTrip_DeltaFramePreservesChangedFields()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(150m), TickerContext.Default.TickerDto);
        var delta = encoder.EncodeChanges(Tick(151m));

        var wire = WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch);
        var decoded = WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(wire);

        Assert.IsType<DeltaFrame<TickerDto>>(decoded);
        var df = (DeltaFrame<TickerDto>)decoded;
        Assert.Equal(encoder.MainFrame.EncoderUuid, df.EncoderUuid);
        Assert.Equal(1UL, df.Version);

        // Apply the deserialized patch to the original value
        var result = df.Patch.ApplyPatch(Tick(150m));
        Assert.Equal(151m, result.Price);
        Assert.Equal(150.99m, result.Bid);
        Assert.Equal(151.01m, result.Ask);
    }

    [Fact]
    public void JsonWireRoundTrip_FullSession_ProducerToConsumer()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(Tick(100m), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        // Simulate: serialize over wire → deserialize → feed to consumer
        var kfWire = WireCodec.EncodeKeyFrame(encoder.MainFrame);
        consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(kfWire));

        var prices = new[] { 101m, 102m, 99m, 103m, 100.5m };
        foreach (var px in prices)
        {
            var delta = encoder.EncodeChanges(Tick(px));
            var wire = WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch);
            consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(wire));
        }

        Assert.Equal(100.5m, consumer.CurrentValue!.Price);
        Assert.Equal(6, consumer.FramesApplied);
        Assert.Equal(0, consumer.FramesRejected);
    }
}
