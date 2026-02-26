using Xunit;

namespace DeltaStreamNet.Tests;

public class MultiProducerTests
{
    private static TickerDto Tick(string sym, decimal price) =>
        new() { Symbol = sym, Price = price, Bid = price - 0.01m, Ask = price + 0.01m, Volume = 100 };

    [Fact]
    public void TwoProducers_ConsumerDetectsUuidMismatch()
    {
        var encoderA = new DeltaStreamEncoder<TickerDto>(Tick("AAPL", 150m), TickerContext.Default.TickerDto);
        var encoderB = new DeltaStreamEncoder<TickerDto>(Tick("MSFT", 300m), TickerContext.Default.TickerDto);

        var consumer = new StreamConsumer<TickerDto>();

        // Subscribe to producer A
        consumer.ApplyFrame(encoderA.MainFrame);
        Assert.Equal("AAPL", consumer.CurrentValue!.Symbol);

        // A delta from producer B arrives (wrong UUID)
        var deltaB = encoderB.EncodeChanges(Tick("MSFT", 301m));
        consumer.ApplyFrame(deltaB);

        Assert.Equal(1, consumer.FramesRejected);
        Assert.Equal("AAPL", consumer.CurrentValue!.Symbol); // unchanged
    }

    [Fact]
    public void TwoProducers_PartitionedConsumers_EachTracksOwnStream()
    {
        var encoderA = new DeltaStreamEncoder<TickerDto>(Tick("AAPL", 150m), TickerContext.Default.TickerDto);
        var encoderB = new DeltaStreamEncoder<TickerDto>(Tick("MSFT", 300m), TickerContext.Default.TickerDto);

        var consumerA = new StreamConsumer<TickerDto>();
        var consumerB = new StreamConsumer<TickerDto>();

        consumerA.ApplyFrame(encoderA.MainFrame);
        consumerB.ApplyFrame(encoderB.MainFrame);

        // Interleaved updates
        consumerA.ApplyFrame(encoderA.EncodeChanges(Tick("AAPL", 151m)));
        consumerB.ApplyFrame(encoderB.EncodeChanges(Tick("MSFT", 301m)));
        consumerA.ApplyFrame(encoderA.EncodeChanges(Tick("AAPL", 152m)));
        consumerB.ApplyFrame(encoderB.EncodeChanges(Tick("MSFT", 299m)));

        Assert.Equal(152m, consumerA.CurrentValue!.Price);
        Assert.Equal(299m, consumerB.CurrentValue!.Price);
        Assert.Equal(0, consumerA.FramesRejected);
        Assert.Equal(0, consumerB.FramesRejected);
    }

    [Fact]
    public void ConsumerSwitchesProducer_ViaKeyFrameReset()
    {
        var encoderA = new DeltaStreamEncoder<TickerDto>(Tick("AAPL", 150m), TickerContext.Default.TickerDto);
        var encoderB = new DeltaStreamEncoder<TickerDto>(Tick("MSFT", 300m), TickerContext.Default.TickerDto);

        var consumer = new StreamConsumer<TickerDto>();
        consumer.ApplyFrame(encoderA.MainFrame);
        consumer.ApplyFrame(encoderA.EncodeChanges(Tick("AAPL", 155m)));
        Assert.Equal("AAPL", consumer.CurrentValue!.Symbol);

        // Consumer switches to producer B: reset + new key frame
        consumer.Reset();
        consumer.ApplyFrame(encoderB.MainFrame);
        Assert.Equal("MSFT", consumer.CurrentValue!.Symbol);
        Assert.Equal(300m, consumer.CurrentValue!.Price);

        // Can now receive deltas from B
        consumer.ApplyFrame(encoderB.EncodeChanges(Tick("MSFT", 310m)));
        Assert.Equal(310m, consumer.CurrentValue!.Price);
    }
}
