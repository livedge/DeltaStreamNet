using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonNestedTests
{
    [Fact]
    public void NestedKeyFrame_BothLevelsHaveMinifiedNames()
    {
        // Outer: Label→"l", Child→"c"
        var labelProp = typeof(MinifiedOuterDtoKeyFrame).GetProperty("Label")!;
        var labelAttr = labelProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("l", labelAttr.Name);

        var childProp = typeof(MinifiedOuterDtoKeyFrame).GetProperty("Child")!;
        var childAttr = childProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("c", childAttr.Name);

        // Inner: Tag→"t", Value→"v"
        var tagProp = typeof(MinifiedInnerDtoKeyFrame).GetProperty("Tag")!;
        var tagAttr = tagProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("t", tagAttr.Name);

        var valProp = typeof(MinifiedInnerDtoKeyFrame).GetProperty("Value")!;
        var valAttr = valProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("v", valAttr.Name);
    }

    [Fact]
    public void NestedDeltaFrame_BothLevelsHaveMinifiedNames()
    {
        // Outer delta: Label→"l", Child→"c"
        var labelProp = typeof(MinifiedOuterDtoDeltaFrame).GetProperty("Label")!;
        var labelAttr = labelProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("l", labelAttr.Name);

        var childProp = typeof(MinifiedOuterDtoDeltaFrame).GetProperty("Child")!;
        var childAttr = childProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("c", childAttr.Name);
    }

    [Fact]
    public void Nested_JsonSerialization_OuterUsesShortNames()
    {
        var kf = MinifiedOuterDtoKeyFrame.From(new MinifiedOuterDto
        {
            Label = "outer",
            Child = new MinifiedInnerDto { Tag = "x", Value = 42 }
        });

        var json = JsonSerializer.Serialize(kf);

        // Outer KeyFrame properties have minified names
        Assert.Contains("\"l\":", json);
        Assert.Contains("\"c\":", json);
        Assert.DoesNotContain("\"Label\"", json);
        Assert.DoesNotContain("\"Child\"", json);
        // Inner value is stored as the raw DTO type (MinifiedInnerDto), so
        // its properties keep their original names. Minified names only apply
        // to the generated KeyFrame/DeltaFrame classes themselves.
        Assert.Contains("\"Tag\"", json);
        Assert.Contains("\"Value\"", json);
    }

    [Fact]
    public void Nested_InnerKeyFrame_UsesShortNamesWhenSerializedDirectly()
    {
        var innerKf = MinifiedInnerDtoKeyFrame.From(
            new MinifiedInnerDto { Tag = "x", Value = 42 });

        var json = JsonSerializer.Serialize(innerKf);

        // When the inner KeyFrame class is serialized directly, it uses minified names
        Assert.Contains("\"t\":", json);
        Assert.Contains("\"v\":", json);
        Assert.DoesNotContain("\"Tag\"", json);
        Assert.DoesNotContain("\"Value\"", json);
    }

    [Fact]
    public void Nested_EncoderDecoder_RoundTrip_OnlyChildChanged()
    {
        var initial = new MinifiedOuterDto
        {
            Label = "outer",
            Child = new MinifiedInnerDto { Tag = "a", Value = 1 }
        };
        var encoder = new DeltaStreamEncoder<MinifiedOuterDto>(initial, MinifiedOuterContext.Default.MinifiedOuterDto);
        var decoder = new DeltaStreamDecoder<MinifiedOuterDto>(encoder.MainFrame);

        var updated = new MinifiedOuterDto
        {
            Label = "outer",
            Child = new MinifiedInnerDto { Tag = "a", Value = 99 }
        };
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("outer", result!.Label);
        Assert.Equal("a", result.Child.Tag);
        Assert.Equal(99, result.Child.Value);
    }

    [Fact]
    public void Nested_DeltaJson_OmitsUnchangedOuterPreservesNestedDelta()
    {
        var prev = MinifiedOuterDtoKeyFrame.From(new MinifiedOuterDto
        {
            Label = "outer",
            Child = new MinifiedInnerDto { Tag = "a", Value = 1 }
        });
        var curr = MinifiedOuterDtoKeyFrame.From(new MinifiedOuterDto
        {
            Label = "outer",
            Child = new MinifiedInnerDto { Tag = "a", Value = 99 }
        });

        var delta = MinifiedOuterDtoDeltaFrame.Create(prev, curr);
        var json = JsonSerializer.Serialize(delta);

        // Label unchanged → null (omitted with WhenWritingNull)
        Assert.Null(delta.Label);
        // Child changed
        Assert.NotNull(delta.Child);
        Assert.True(delta.Child!.HasChanges);
        // HasChanges not in JSON
        Assert.DoesNotContain("HasChanges", json);
    }

    [Fact]
    public void Nested_MultipleDeltas_StateAccumulatesCorrectly()
    {
        var initial = new MinifiedOuterDto
        {
            Label = "v1",
            Child = new MinifiedInnerDto { Tag = "x", Value = 10 }
        };
        var encoder = new DeltaStreamEncoder<MinifiedOuterDto>(initial, MinifiedOuterContext.Default.MinifiedOuterDto);
        var decoder = new DeltaStreamDecoder<MinifiedOuterDto>(encoder.MainFrame);

        // Delta 1: only child value changes
        var d1 = encoder.EncodeChanges(new MinifiedOuterDto
        {
            Label = "v1",
            Child = new MinifiedInnerDto { Tag = "x", Value = 20 }
        });
        var r1 = decoder.DecodeFrame(d1);
        Assert.Equal("v1", r1!.Label);
        Assert.Equal(20, r1.Child.Value);

        // Delta 2: only label changes
        var d2 = encoder.EncodeChanges(new MinifiedOuterDto
        {
            Label = "v2",
            Child = new MinifiedInnerDto { Tag = "x", Value = 20 }
        });
        var r2 = decoder.DecodeFrame(d2);
        Assert.Equal("v2", r2!.Label);
        Assert.Equal(20, r2.Child.Value);

        // Delta 3: both change
        var d3 = encoder.EncodeChanges(new MinifiedOuterDto
        {
            Label = "v3",
            Child = new MinifiedInnerDto { Tag = "y", Value = 30 }
        });
        var r3 = decoder.DecodeFrame(d3);
        Assert.Equal("v3", r3!.Label);
        Assert.Equal("y", r3.Child.Tag);
        Assert.Equal(30, r3.Child.Value);
    }
}
