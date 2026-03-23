using System.Text.Json;
using Xunit;

namespace DeltaStreamNet.Tests;

public class ClosedGenericTests
{
    [Fact]
    public void Context_exposes_closed_generic_generator()
    {
        var generator = GenericContext.Default.WrapperDtoOfInnerDto;
        Assert.NotNull(generator);
        Assert.IsAssignableFrom<IDeltaFrameGenerator<WrapperDto<InnerDto>>>(generator);
    }

    [Fact]
    public void Encoder_produces_keyframe_for_closed_generic()
    {
        var initial = new WrapperDto<InnerDto>
        {
            Data = new InnerDto { Tag = "a", Value = 1 },
            Version = 1,
            Active = true
        };

        var encoder = new DeltaStreamEncoder<WrapperDto<InnerDto>>(
            initial, GenericContext.Default.WrapperDtoOfInnerDto);

        Assert.NotNull(encoder.MainFrame);
        Assert.Equal(initial, encoder.MainFrame.Value);
    }

    [Fact]
    public void Delta_captures_inner_dto_changes()
    {
        var v1 = new WrapperDto<InnerDto>
        {
            Data = new InnerDto { Tag = "a", Value = 1 },
            Version = 1,
            Active = true
        };

        var encoder = new DeltaStreamEncoder<WrapperDto<InnerDto>>(
            v1, GenericContext.Default.WrapperDtoOfInnerDto);

        var v2 = new WrapperDto<InnerDto>
        {
            Data = new InnerDto { Tag = "a", Value = 99 },
            Version = 2,
            Active = true
        };

        var delta = encoder.EncodeChanges(v2);
        Assert.True(delta.Patch is not null);

        // Apply patch
        var result = delta.Patch.ApplyPatch(v1);
        Assert.Equal(99, result.Data.Value);
        Assert.Equal(2, result.Version);
        Assert.Equal("a", result.Data.Tag);
    }

    [Fact]
    public void Json_roundtrip_keyframe()
    {
        var initial = new WrapperDto<InnerDto>
        {
            Data = new InnerDto { Tag = "hello", Value = 42 },
            Version = 5,
            Active = true
        };

        var encoder = new DeltaStreamEncoder<WrapperDto<InnerDto>>(
            initial, GenericContext.Default.WrapperDtoOfInnerDto);

        var json = JsonSerializer.Serialize<Frame<WrapperDto<InnerDto>>>(encoder.MainFrame);
        var deserialized = JsonSerializer.Deserialize<Frame<WrapperDto<InnerDto>>>(json);

        Assert.IsType<KeyFrame<WrapperDto<InnerDto>>>(deserialized);
        var kf = (KeyFrame<WrapperDto<InnerDto>>)deserialized!;
        Assert.Equal("hello", kf.Value.Data.Tag);
        Assert.Equal(42, kf.Value.Data.Value);
        Assert.Equal(5, kf.Value.Version);
    }

    [Fact]
    public void Json_roundtrip_delta()
    {
        var v1 = new WrapperDto<InnerDto>
        {
            Data = new InnerDto { Tag = "a", Value = 1 },
            Version = 1,
            Active = true
        };

        var encoder = new DeltaStreamEncoder<WrapperDto<InnerDto>>(
            v1, GenericContext.Default.WrapperDtoOfInnerDto);

        var v2 = v1 with { Version = 2, Data = new InnerDto { Tag = "a", Value = 2 } };
        var delta = encoder.EncodeChanges(v2);

        var json = JsonSerializer.Serialize<Frame<WrapperDto<InnerDto>>>(delta);
        var deserialized = JsonSerializer.Deserialize<Frame<WrapperDto<InnerDto>>>(json);

        Assert.IsType<DeltaFrame<WrapperDto<InnerDto>>>(deserialized);
        var df = (DeltaFrame<WrapperDto<InnerDto>>)deserialized!;
        var result = df.Patch.ApplyPatch(v1);
        Assert.Equal(2, result.Data.Value);
        Assert.Equal(2, result.Version);
    }

    [Fact]
    public void Full_consumer_flow_with_closed_generic()
    {
        var v1 = new WrapperDto<InnerDto>
        {
            Data = new InnerDto { Tag = "start", Value = 0 },
            Version = 1,
            Active = true
        };

        var encoder = new DeltaStreamEncoder<WrapperDto<InnerDto>>(
            v1, GenericContext.Default.WrapperDtoOfInnerDto);

        var consumer = new StreamConsumer<WrapperDto<InnerDto>>();
        consumer.ApplyFrame(encoder.MainFrame);
        Assert.Equal(0, consumer.CurrentValue!.Data.Value);

        for (var i = 1; i <= 5; i++)
        {
            var next = new WrapperDto<InnerDto>
            {
                Data = new InnerDto { Tag = "start", Value = i },
                Version = i + 1,
                Active = true
            };
            consumer.ApplyFrame(encoder.EncodeChanges(next));
        }

        Assert.Equal(5, consumer.CurrentValue!.Data.Value);
        Assert.Equal(6, consumer.CurrentValue!.Version);
        Assert.Equal(6, consumer.FramesApplied);
    }

    [Fact]
    public void Non_generic_dto_still_works_in_same_context()
    {
        var generator = GenericContext.Default.InnerDto;
        Assert.NotNull(generator);
        Assert.IsAssignableFrom<IDeltaFrameGenerator<InnerDto>>(generator);

        var encoder = new DeltaStreamEncoder<InnerDto>(
            new InnerDto { Tag = "x", Value = 1 }, generator);
        var delta = encoder.EncodeChanges(new InnerDto { Tag = "x", Value = 2 });
        Assert.NotNull(delta.Patch);
    }
}
