using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class NullableDeltaModeTests
{
    [Fact]
    public void DeltaFrame_UnchangedProperties_AreNull()
    {
        var prev = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });
        var curr = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 200 });

        var delta = ProtobufAnnotatedDtoDeltaFrame.Create(prev, curr);

        Assert.Null(delta.Name);               // unchanged → null
        Assert.Equal(200, delta.Score!.Value.Value); // changed → has value
    }

    [Fact]
    public void DeltaFrame_ChangedProperty_HasValue()
    {
        var prev = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });
        var curr = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Bob", Score = 100 });

        var delta = ProtobufAnnotatedDtoDeltaFrame.Create(prev, curr);

        Assert.Equal("Bob", delta.Name!.Value.Value); // changed → has value
        Assert.Null(delta.Score);                     // unchanged → null
    }

    [Fact]
    public void DeltaFrame_AllChanged_HasChangesTrue()
    {
        var prev = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });
        var curr = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Bob", Score = 200 });

        var delta = ProtobufAnnotatedDtoDeltaFrame.Create(prev, curr);

        Assert.True(delta.HasChanges);
    }

    [Fact]
    public void DeltaFrame_NoneChanged_HasChangesFalse()
    {
        var prev = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });
        var curr = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });

        var delta = ProtobufAnnotatedDtoDeltaFrame.Create(prev, curr);

        Assert.False(delta.HasChanges);
    }

    [Fact]
    public void DeltaFrame_ApplyPatch_PreservesUnchangedAndUpdatesChanged()
    {
        var initial = new ProtobufAnnotatedDto { Name = "Alice", Score = 100 };
        var updated = new ProtobufAnnotatedDto { Name = "Alice", Score = 200 };

        var encoder = new DeltaStreamEncoder<ProtobufAnnotatedDto>(initial, ProtobufAnnotatedContext.Default.ProtobufAnnotatedDto);
        var delta = encoder.EncodeChanges(updated);
        var result = delta.Patch.ApplyPatch(initial);

        Assert.Equal("Alice", result.Name);
        Assert.Equal(200, result.Score);
    }

    [Fact]
    public void DeltaFrame_ShouldSerialize_ReturnsFalseForUnchanged()
    {
        var prev = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 100 });
        var curr = ProtobufAnnotatedDtoKeyFrame.From(new ProtobufAnnotatedDto { Name = "Alice", Score = 200 });

        var delta = ProtobufAnnotatedDtoDeltaFrame.Create(prev, curr);

        Assert.Null(delta.Name);
        Assert.NotNull(delta.Score);
    }

    [Fact]
    public void DeltaFrame_JsonOmitsUnchangedProperties()
    {
        var prev = AnnotatedDtoKeyFrame.From(new AnnotatedDto { Name = "Alice", Score = 100 });
        var curr = AnnotatedDtoKeyFrame.From(new AnnotatedDto { Name = "Alice", Score = 200 });

        var delta = AnnotatedDtoDeltaFrame.Create(prev, curr);

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(delta, options);

        // Only Score should appear; Name should be omitted (unchanged → null)
        Assert.Contains("player_score", json);
        Assert.DoesNotContain("player_name", json);
        // HasChanges should not appear (it's [JsonIgnore])
        Assert.DoesNotContain("HasChanges", json);
    }

    [Fact]
    public void DeltaFrame_JsonPropagatesPropertyNames()
    {
        // Verify DeltaFrame properties carry propagated [JsonPropertyName] attrs
        var prop = typeof(AnnotatedDtoDeltaFrame).GetProperty(nameof(AnnotatedDtoDeltaFrame.Name))!;
        var attr = prop.GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                       .Cast<JsonPropertyNameAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("player_name", attr!.Name);
    }

    [Fact]
    public void EncoderDecoder_RoundTrip_WorksWithNullableMode()
    {
        var encoder = new DeltaStreamEncoder<ProtobufAnnotatedDto>(
            new ProtobufAnnotatedDto { Name = "Alice", Score = 100 },
            ProtobufAnnotatedContext.Default.ProtobufAnnotatedDto);
        var decoder = new DeltaStreamDecoder<ProtobufAnnotatedDto>(encoder.MainFrame);

        var delta1 = encoder.EncodeChanges(new ProtobufAnnotatedDto { Name = "Alice", Score = 200 });
        var delta2 = encoder.EncodeChanges(new ProtobufAnnotatedDto { Name = "Bob", Score = 200 });
        var delta3 = encoder.EncodeChanges(new ProtobufAnnotatedDto { Name = "Bob", Score = 300 });

        Assert.Equal(200, decoder.DecodeFrame(delta1)!.Score);
        Assert.Equal("Bob", decoder.DecodeFrame(delta2)!.Name);
        Assert.Equal(300, decoder.DecodeFrame(delta3)!.Score);
    }
}
