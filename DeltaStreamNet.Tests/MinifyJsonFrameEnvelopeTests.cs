using System.Text.Json;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonFrameEnvelopeTests
{
    [Fact]
    public void KeyFrame_EnvelopeUsesShortNames()
    {
        var initial = new MinifiedDto
            { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 };
        var encoder = new DeltaStreamEncoder<MinifiedDto>(initial, MinifiedContext.Default.MinifiedDto);

        var json = JsonSerializer.Serialize(encoder.MainFrame);

        // Envelope fields
        Assert.Contains("\"u\":", json);  // EncoderUuid
        Assert.Contains("\"v\":", json);  // Version
        Assert.Contains("\"t\":", json);  // Timestamp
        Assert.Contains("\"d\":", json);  // Value
        Assert.DoesNotContain("\"EncoderUuid\"", json);
        Assert.DoesNotContain("\"Version\"", json);
        Assert.DoesNotContain("\"Timestamp\"", json);
        Assert.DoesNotContain("\"Value\"", json);
    }

    [Fact]
    public void DeltaFrame_EnvelopeUsesShortNames()
    {
        var initial = new MinifiedDto
            { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 };
        var encoder = new DeltaStreamEncoder<MinifiedDto>(initial, MinifiedContext.Default.MinifiedDto);
        var delta = encoder.EncodeChanges(new MinifiedDto
            { Name = "Bob", Score = 100, Price = 9.99, IsActive = true, Id = 1 });

        var json = JsonSerializer.Serialize(delta);

        Assert.Contains("\"u\":", json);
        Assert.Contains("\"v\":", json);
        Assert.Contains("\"t\":", json);
        Assert.Contains("\"p\":", json);  // Patch
        Assert.DoesNotContain("\"EncoderUuid\"", json);
        Assert.DoesNotContain("\"Patch\"", json);
    }

    [Fact]
    public void PropertyDeltaWrapper_UsesShortNames()
    {
        var wrapper = new PropertyDeltaWrapper<int> { Value = 42 };
        var json = JsonSerializer.Serialize(wrapper);

        Assert.Contains("\"v\":", json);
        Assert.DoesNotContain("\"Value\"", json);
    }
}
