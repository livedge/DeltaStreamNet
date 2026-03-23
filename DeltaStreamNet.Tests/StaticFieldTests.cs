using System;
using System.Text.Json;
using Xunit;

namespace DeltaStreamNet.Tests;

[StreamFrame]
public record StaticFieldDto
{
    [StreamField(Static = true)]
    public required string Id { get; set; }

    [StreamField(Static = true)]
    public required string Category { get; set; }

    public required int Score { get; set; }
    public required decimal Price { get; set; }
}

[DeltaStreamSerializable(typeof(StaticFieldDto))]
public partial class StaticFieldContext : DeltaStreamContext;

public class StaticFieldTests
{
    [Fact]
    public void Delta_skips_static_fields_when_unchanged()
    {
        var v1 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 10, Price = 1.5m };
        var v2 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 20, Price = 1.5m };

        var encoder = new DeltaStreamEncoder<StaticFieldDto>(v1, StaticFieldContext.Default.StaticFieldDto);
        var delta = encoder.EncodeChanges(v2);

        var json = JsonSerializer.Serialize<Frame<StaticFieldDto>>(delta);

        // Static fields should not appear in the delta at all
        Assert.DoesNotContain("\"Id\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Category\"", json, StringComparison.OrdinalIgnoreCase);
        // Dynamic field that changed should have a value
        Assert.Contains("Score", json);
        Assert.Contains("\"v\":20", json);
    }

    [Fact]
    public void Delta_skips_static_fields_even_when_different()
    {
        // Static means "never tracked" — even if values differ, no delta is produced
        var v1 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 10, Price = 1.5m };
        var v2 = new StaticFieldDto { Id = "CHANGED", Category = "CHANGED", Score = 10, Price = 1.5m };

        var encoder = new DeltaStreamEncoder<StaticFieldDto>(v1, StaticFieldContext.Default.StaticFieldDto);
        var delta = encoder.EncodeChanges(v2);

        var json = JsonSerializer.Serialize<Frame<StaticFieldDto>>(delta);
        Assert.DoesNotContain("Id", json);
        Assert.DoesNotContain("Category", json);
    }

    [Fact]
    public void ApplyPatch_preserves_static_fields_from_previous()
    {
        var v1 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 10, Price = 1.5m };
        var v2 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 99, Price = 2.0m };

        var encoder = new DeltaStreamEncoder<StaticFieldDto>(v1, StaticFieldContext.Default.StaticFieldDto);
        var delta = encoder.EncodeChanges(v2);

        var result = delta.Patch.ApplyPatch(v1);

        Assert.Equal("abc", result.Id);
        Assert.Equal("sports", result.Category);
        Assert.Equal(99, result.Score);
        Assert.Equal(2.0m, result.Price);
    }

    [Fact]
    public void KeyFrame_includes_static_fields()
    {
        var v1 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 10, Price = 1.5m };

        var encoder = new DeltaStreamEncoder<StaticFieldDto>(v1, StaticFieldContext.Default.StaticFieldDto);
        var json = JsonSerializer.Serialize<Frame<StaticFieldDto>>(encoder.MainFrame);

        Assert.Contains("abc", json);
        Assert.Contains("sports", json);
    }

    [Fact]
    public void Json_roundtrip_with_static_fields()
    {
        var v1 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 10, Price = 1.5m };
        var v2 = new StaticFieldDto { Id = "abc", Category = "sports", Score = 50, Price = 3.0m };

        var encoder = new DeltaStreamEncoder<StaticFieldDto>(v1, StaticFieldContext.Default.StaticFieldDto);
        var delta = encoder.EncodeChanges(v2);

        var json = JsonSerializer.Serialize<Frame<StaticFieldDto>>(delta);
        var deserialized = JsonSerializer.Deserialize<Frame<StaticFieldDto>>(json);

        var df = Assert.IsType<DeltaFrame<StaticFieldDto>>(deserialized);
        var result = df.Patch.ApplyPatch(v1);

        Assert.Equal("abc", result.Id);
        Assert.Equal("sports", result.Category);
        Assert.Equal(50, result.Score);
        Assert.Equal(3.0m, result.Price);
    }
}
