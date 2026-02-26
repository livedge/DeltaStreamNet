using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonMixedModeTests
{
    [Fact]
    public void MixedParent_MinifyJsonOverridesUserJsonPropertyName()
    {
        // MixedParentDto has [JsonPropertyName("long_label")] on Label
        // but also MinifyJson=true, so the auto short name should win
        var labelProp = typeof(MixedParentDtoKeyFrame).GetProperty("Label")!;
        var attrs = labelProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().ToList();

        Assert.Single(attrs);
        Assert.Equal("l", attrs[0].Name);
    }

    [Fact]
    public void MixedParent_EncoderDecoder_RoundTrip()
    {
        var initial = new MixedParentDto
        {
            Label = "hello",
            Child = new MinifiedInnerDto { Tag = "t1", Value = 10 }
        };
        var encoder = new DeltaStreamEncoder<MixedParentDto>(initial, MixedParentContext.Default.MixedParentDto);
        var decoder = new DeltaStreamDecoder<MixedParentDto>(encoder.MainFrame);

        var updated = new MixedParentDto
        {
            Label = "hello",
            Child = new MinifiedInnerDto { Tag = "t2", Value = 10 }
        };
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("hello", result!.Label);
        Assert.Equal("t2", result.Child.Tag);
    }

    [Fact]
    public void MixedParent_DeltaJson_UsesShortNames()
    {
        var initial = new MixedParentDto
        {
            Label = "hello",
            Child = new MinifiedInnerDto { Tag = "t1", Value = 10 }
        };
        var encoder = new DeltaStreamEncoder<MixedParentDto>(initial, MixedParentContext.Default.MixedParentDto);

        var updated = new MixedParentDto
        {
            Label = "world",
            Child = new MinifiedInnerDto { Tag = "t1", Value = 10 }
        };
        var delta = encoder.EncodeChanges(updated);
        var patch = (MixedParentDtoDeltaFrame)delta.Patch;

        var opts = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json = JsonSerializer.Serialize(patch, opts);

        // Label changed → present with short name
        Assert.Contains("\"l\":", json);
        Assert.DoesNotContain("\"Label\"", json);
        Assert.DoesNotContain("long_label", json);
        // Child unchanged → should be null / omitted
        Assert.DoesNotContain("\"c\":", json);
    }
}
