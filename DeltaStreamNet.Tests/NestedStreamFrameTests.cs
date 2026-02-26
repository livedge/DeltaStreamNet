using Xunit;

namespace DeltaStreamNet.Tests;

public class NestedStreamFrameTests
{
    [Fact]
    public void EncodeAndDecode_NestedPropertyChanged_AppliesNestedDelta()
    {
        var initial = new ParentDto
        {
            Label = "outer",
            Child = new NestedDto { Tag = "a", Value = 1 }
        };
        var encoder = new DeltaStreamEncoder<ParentDto>(initial, ParentContext.Default.ParentDto);
        var decoder = new DeltaStreamDecoder<ParentDto>(encoder.MainFrame);

        var updated = new ParentDto
        {
            Label = "outer",
            Child = new NestedDto { Tag = "a", Value = 99 }
        };

        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("outer", result!.Label);
        Assert.Equal("a",     result.Child.Tag);
        Assert.Equal(99,      result.Child.Value);
    }

    [Fact]
    public void EncodeAndDecode_OuterChangedNestedUnchanged_PreservesNested()
    {
        var initial = new ParentDto
        {
            Label = "outer",
            Child = new NestedDto { Tag = "a", Value = 1 }
        };
        var encoder = new DeltaStreamEncoder<ParentDto>(initial, ParentContext.Default.ParentDto);
        var decoder = new DeltaStreamDecoder<ParentDto>(encoder.MainFrame);

        var updated = new ParentDto
        {
            Label = "changed",
            Child = new NestedDto { Tag = "a", Value = 1 }
        };

        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("changed", result!.Label);
        Assert.Equal("a",       result.Child.Tag);
        Assert.Equal(1,         result.Child.Value);
    }

    [Fact]
    public void RoundTrip_BothLevelsChange_AllValuesUpdated()
    {
        var initial = new ParentDto
        {
            Label = "v1",
            Child = new NestedDto { Tag = "x", Value = 10 }
        };
        var encoder = new DeltaStreamEncoder<ParentDto>(initial, ParentContext.Default.ParentDto);
        var decoder = new DeltaStreamDecoder<ParentDto>(encoder.MainFrame);

        var updated = new ParentDto
        {
            Label = "v2",
            Child = new NestedDto { Tag = "y", Value = 20 }
        };

        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("v2", result!.Label);
        Assert.Equal("y",  result.Child.Tag);
        Assert.Equal(20,   result.Child.Value);
    }
}
