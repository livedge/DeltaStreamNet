using Xunit;

namespace DeltaStreamNet.Tests;

public class JsonWireFormatIntegrationTests
{
    private static TickerDto Tick(string sym, decimal px, long vol) =>
        new() { Symbol = sym, Price = px, Bid = px - 0.01m, Ask = px + 0.01m, Volume = vol };

    [Fact]
    public void WirePayload_KeyFrame_UsesMinifiedNames()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(
            Tick("AAPL", 150m, 5000), TickerContext.Default.TickerDto);

        var wire = WireCodec.EncodeKeyFrame(encoder.MainFrame);
        var json = System.Text.Encoding.UTF8.GetString(wire.Payload);

        // DTO properties use auto-minified names (from MinifyJson=true)
        // TickerDto has PropagateAttributes, so the user-supplied [JsonPropertyName] is filtered
        // and replaced with auto-generated short names
        Assert.DoesNotContain("\"Symbol\"", json);
        Assert.DoesNotContain("\"Price\"", json);
        Assert.DoesNotContain("\"Volume\"", json);
    }

    [Fact]
    public void WirePayload_DeltaFrame_OnlyContainsChangedFields()
    {
        var encoder = new DeltaStreamEncoder<TickerDto>(
            Tick("AAPL", 150m, 5000), TickerContext.Default.TickerDto);
        var delta = encoder.EncodeChanges(Tick("AAPL", 151m, 5000));

        var wire = WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch);
        var json = System.Text.Encoding.UTF8.GetString(wire.Payload);

        // Only Price/Bid/Ask changed — Symbol and Volume should be null (omitted)
        // HasChanges should NOT appear (it's [JsonIgnore])
        Assert.DoesNotContain("HasChanges", json);
    }

    [Fact]
    public void FullSession_ThroughWire_StateMatchesProducer()
    {
        var prices = new[] { 100m, 101m, 99.5m, 102m, 98m, 103m, 103m, 104m };
        var encoder = new DeltaStreamEncoder<TickerDto>(
            Tick("TEST", prices[0], 0), TickerContext.Default.TickerDto);
        var consumer = new StreamConsumer<TickerDto>();

        // KeyFrame through wire
        var kfWire = WireCodec.EncodeKeyFrame(encoder.MainFrame);
        consumer.ApplyFrame(WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(kfWire));

        // Each price update through wire
        for (var i = 1; i < prices.Length; i++)
        {
            var delta = encoder.EncodeChanges(Tick("TEST", prices[i], i));
            var wire = WireCodec.EncodeDeltaFrame(delta, (TickerDtoDeltaFrame)delta.Patch);
            var decoded = WireCodec.Decode<TickerDto, TickerDtoDeltaFrame>(wire);
            consumer.ApplyFrame(decoded);
        }

        Assert.Equal(prices[^1], consumer.CurrentValue!.Price);
        Assert.Equal(prices.Length - 1, (int)consumer.CurrentVersion);
        Assert.Equal("TEST", consumer.CurrentValue!.Symbol);
        Assert.Equal(prices.Length - 1, consumer.CurrentValue!.Volume);
    }
}
