using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonTests
{
    [Fact]
    public void KeyFrame_HasMinifiedJsonPropertyNames()
    {
        var nameProp = typeof(MinifiedDtoKeyFrame).GetProperty("Name")!;
        var nameAttr = nameProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();
        Assert.NotNull(nameAttr);
        Assert.Equal("n", nameAttr!.Name);

        var scoreProp = typeof(MinifiedDtoKeyFrame).GetProperty("Score")!;
        var scoreAttr = scoreProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();
        Assert.NotNull(scoreAttr);
        Assert.Equal("s", scoreAttr!.Name);

        var priceProp = typeof(MinifiedDtoKeyFrame).GetProperty("Price")!;
        var priceAttr = priceProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();
        Assert.NotNull(priceAttr);
        Assert.Equal("p", priceAttr!.Name);
    }

    [Fact]
    public void DeltaFrame_HasMinifiedJsonPropertyNames()
    {
        var scoreProp = typeof(MinifiedDtoDeltaFrame).GetProperty("Score")!;
        var scoreAttr = scoreProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();
        Assert.NotNull(scoreAttr);
        Assert.Equal("s", scoreAttr!.Name);
    }

    [Fact]
    public void CollisionDto_ResolvesWithLongerPrefix()
    {
        // Price and Product both start with 'p', so they get 'pr' and 'pro' (or similar)
        var priceProp = typeof(CollisionDtoKeyFrame).GetProperty("Price")!;
        var priceAttr = priceProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();

        var productProp = typeof(CollisionDtoKeyFrame).GetProperty("Product")!;
        var productAttr = productProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();

        Assert.NotNull(priceAttr);
        Assert.NotNull(productAttr);
        // They must be different
        Assert.NotEqual(priceAttr!.Name, productAttr!.Name);
        // Name should still be 'n' (no collision)
        var nameProp = typeof(CollisionDtoKeyFrame).GetProperty("Name")!;
        var nameAttr = nameProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().SingleOrDefault();
        Assert.NotNull(nameAttr);
        Assert.Equal("n", nameAttr!.Name);
    }

    [Fact]
    public void MinifyJson_WithPropagateAttributes_FiltersUserJsonPropertyName()
    {
        // When both MinifyJson and PropagateAttributes are on, the auto short name wins
        // and the user's [JsonPropertyName("player_name")] is filtered out
        var nameProp = typeof(MinifiedAnnotatedDtoKeyFrame).GetProperty("Name")!;
        var attrs = nameProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().ToList();

        // Should have exactly one [JsonPropertyName] — the auto-generated short one
        Assert.Single(attrs);
        Assert.Equal("n", attrs[0].Name);

        // But other non-JsonPropertyName attributes should still be propagated
        var descAttrs = nameProp.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
        Assert.Single(descAttrs);
    }

    [Fact]
    public void MinifiedDto_JsonRoundTrip_UsesShortNames()
    {
        var keyFrame = MinifiedDtoKeyFrame.From(new MinifiedDto
        {
            Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1
        });

        var json = JsonSerializer.Serialize(keyFrame);

        Assert.Contains("\"n\":", json);
        Assert.Contains("\"s\":", json);
        Assert.Contains("\"p\":", json);
        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Score\"", json);
    }

    [Fact]
    public void DeltaFrame_HasChanges_NotInJson()
    {
        var prev = MinifiedDtoKeyFrame.From(new MinifiedDto
            { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var curr = MinifiedDtoKeyFrame.From(new MinifiedDto
            { Name = "Alice", Score = 200, Price = 9.99, IsActive = true, Id = 1 });
        var delta = MinifiedDtoDeltaFrame.Create(prev, curr);

        var json = JsonSerializer.Serialize(delta);

        Assert.DoesNotContain("HasChanges", json);
    }
}
