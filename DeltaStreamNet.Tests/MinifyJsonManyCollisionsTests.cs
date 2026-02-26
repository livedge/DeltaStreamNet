using System.Linq;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonManyCollisionsTests
{
    [Fact]
    public void AllPropertyNames_AreUnique()
    {
        var type = typeof(ManyCollisionsDtoKeyFrame);
        var propNames = new[] { "Padding", "Password", "Path", "Quantity", "Quality", "Active" };

        var shortNames = propNames.Select(name =>
        {
            var prop = type.GetProperty(name)!;
            return prop.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                .Cast<JsonPropertyNameAttribute>().Single().Name;
        }).ToList();

        // All short names must be distinct
        Assert.Equal(shortNames.Count, shortNames.Distinct().Count());
    }

    [Fact]
    public void NonCollidingProperty_GetsShortestPrefix()
    {
        // "Active" is the only 'a' prefix → should be "a"
        var activeProp = typeof(ManyCollisionsDtoKeyFrame).GetProperty("Active")!;
        var activeAttr = activeProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("a", activeAttr.Name);
    }

    [Fact]
    public void CollidingProperties_GetProgressivelyLongerPrefixes()
    {
        // Padding, Password, Path all start with 'pa'
        // → need at least 3 chars: "pad", "pas", "pat"
        var type = typeof(ManyCollisionsDtoKeyFrame);

        var paddingAttr = type.GetProperty("Padding")!
            .GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var passwordAttr = type.GetProperty("Password")!
            .GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var pathAttr = type.GetProperty("Path")!
            .GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        // All three must be unique
        var names = new[] { paddingAttr.Name, passwordAttr.Name, pathAttr.Name };
        Assert.Equal(3, names.Distinct().Count());

        // All should be longer than 2 chars (since "pa" collides)
        Assert.All(names, n => Assert.True(n.Length >= 3, $"Expected length >= 3, got \"{n}\""));
    }

    [Fact]
    public void EncoderDecoder_RoundTrip_WithManyCollisions()
    {
        var initial = new ManyCollisionsDto
        {
            Padding = "10px", Password = "secret", Path = "/home",
            Quantity = 5, Quality = 100, Active = true
        };
        var encoder = new DeltaStreamEncoder<ManyCollisionsDto>(initial, ManyCollisionsContext.Default.ManyCollisionsDto);
        var decoder = new DeltaStreamDecoder<ManyCollisionsDto>(encoder.MainFrame);

        var updated = new ManyCollisionsDto
        {
            Padding = "10px", Password = "new-secret", Path = "/home",
            Quantity = 5, Quality = 100, Active = false
        };
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("new-secret", result!.Password);
        Assert.False(result.Active);
        // Unchanged fields preserved
        Assert.Equal("10px", result.Padding);
        Assert.Equal("/home", result.Path);
        Assert.Equal(5, result.Quantity);
        Assert.Equal(100, result.Quality);
    }
}
