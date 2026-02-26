using System.Linq;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class AttributePropagationTests
{
    [Fact]
    public void KeyFrame_PropagatesJsonPropertyNameToNameProperty()
    {
        var prop = typeof(AnnotatedDtoKeyFrame).GetProperty(nameof(AnnotatedDtoKeyFrame.Name))!;
        var attr = prop.GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                       .Cast<JsonPropertyNameAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("player_name", attr!.Name);
    }

    [Fact]
    public void KeyFrame_PropagatesJsonPropertyNameToScoreProperty()
    {
        var prop = typeof(AnnotatedDtoKeyFrame).GetProperty(nameof(AnnotatedDtoKeyFrame.Score))!;
        var attr = prop.GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                       .Cast<JsonPropertyNameAttribute>()
                       .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("player_score", attr!.Name);
    }

    [Fact]
    public void NonAnnotatedKeyFrame_HasNoJsonPropertyNameAttributes()
    {
        var prop = typeof(TestDtoKeyFrame).GetProperty(nameof(TestDtoKeyFrame.Name))!;
        var attrs = prop.GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false);

        Assert.Empty(attrs);
    }
}
