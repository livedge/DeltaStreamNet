using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonThreeLevelNestingTests
{
    private static MinifiedUserDto MakeUser(string name, int age, string city, double lat, double lon) =>
        new()
        {
            Name = name,
            Age = age,
            Address = new MinifiedAddressDto
            {
                City = city,
                Coords = new MinifiedGeoDto { Latitude = lat, Longitude = lon }
            }
        };

    [Fact]
    public void ThreeLevel_KeyFrame_AllLevelsHaveMinifiedNames()
    {
        // User: Name→"n", Age→"ag" (collision with Address→"ad")
        var nameProp = typeof(MinifiedUserDtoKeyFrame).GetProperty("Name")!;
        var nameAttr = nameProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var ageProp = typeof(MinifiedUserDtoKeyFrame).GetProperty("Age")!;
        var ageAttr = ageProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var addrProp = typeof(MinifiedUserDtoKeyFrame).GetProperty("Address")!;
        var addrAttr = addrProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        // Age and Address both start with 'a', so they must differ
        Assert.NotEqual(ageAttr.Name, addrAttr.Name);
        // Name has no collision
        Assert.Equal("n", nameAttr.Name);

        // Address: City→"ci" (collision with Coords→"co") — both start with 'c'
        var cityProp = typeof(MinifiedAddressDtoKeyFrame).GetProperty("City")!;
        var cityAttr = cityProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var coordsProp = typeof(MinifiedAddressDtoKeyFrame).GetProperty("Coords")!;
        var coordsAttr = coordsProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        Assert.NotEqual(cityAttr.Name, coordsAttr.Name);

        // Geo: Latitude→"la" (collision with Longitude→"lo") — both start with 'l'
        var latProp = typeof(MinifiedGeoDtoKeyFrame).GetProperty("Latitude")!;
        var latAttr = latProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var lonProp = typeof(MinifiedGeoDtoKeyFrame).GetProperty("Longitude")!;
        var lonAttr = lonProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        Assert.NotEqual(latAttr.Name, lonAttr.Name);
    }

    [Fact]
    public void ThreeLevel_JsonSerialization_TopLevelUsesShortNames()
    {
        var kf = MinifiedUserDtoKeyFrame.From(MakeUser("Alice", 30, "NYC", 40.7, -74.0));
        var json = JsonSerializer.Serialize(kf);

        // Top-level KeyFrame properties use minified names
        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Age\"", json);
        Assert.DoesNotContain("\"Address\"", json);
        // Nested DTO values (MinifiedAddressDto, MinifiedGeoDto) are raw DTOs
        // stored by type, so their properties keep original names
        Assert.Contains("\"City\"", json);
        Assert.Contains("\"Latitude\"", json);
    }

    [Fact]
    public void ThreeLevel_EachKeyFrame_UsesShortNamesWhenSerializedDirectly()
    {
        // Each generated KeyFrame has minified names on its own properties
        var addrKf = MinifiedAddressDtoKeyFrame.From(
            new MinifiedAddressDto
            {
                City = "NYC",
                Coords = new MinifiedGeoDto { Latitude = 40.7, Longitude = -74.0 }
            });
        var addrJson = JsonSerializer.Serialize(addrKf);
        Assert.DoesNotContain("\"City\"", addrJson);
        Assert.DoesNotContain("\"Coords\"", addrJson);

        var geoKf = MinifiedGeoDtoKeyFrame.From(
            new MinifiedGeoDto { Latitude = 40.7, Longitude = -74.0 });
        var geoJson = JsonSerializer.Serialize(geoKf);
        Assert.DoesNotContain("\"Latitude\"", geoJson);
        Assert.DoesNotContain("\"Longitude\"", geoJson);
    }

    [Fact]
    public void ThreeLevel_EncoderDecoder_DeepLeafChange()
    {
        var initial = MakeUser("Alice", 30, "NYC", 40.7, -74.0);
        var encoder = new DeltaStreamEncoder<MinifiedUserDto>(initial, MinifiedUserContext.Default.MinifiedUserDto);
        var decoder = new DeltaStreamDecoder<MinifiedUserDto>(encoder.MainFrame);

        // Only change the deepest leaf: Longitude
        var updated = MakeUser("Alice", 30, "NYC", 40.7, -73.9);
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("Alice", result!.Name);
        Assert.Equal(30, result.Age);
        Assert.Equal("NYC", result.Address.City);
        Assert.Equal(40.7, result.Address.Coords.Latitude);
        Assert.Equal(-73.9, result.Address.Coords.Longitude);
    }

    [Fact]
    public void ThreeLevel_EncoderDecoder_MiddleLevelChange()
    {
        var initial = MakeUser("Alice", 30, "NYC", 40.7, -74.0);
        var encoder = new DeltaStreamEncoder<MinifiedUserDto>(initial, MinifiedUserContext.Default.MinifiedUserDto);
        var decoder = new DeltaStreamDecoder<MinifiedUserDto>(encoder.MainFrame);

        // Change city but keep coords the same
        var updated = MakeUser("Alice", 30, "LA", 40.7, -74.0);
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("LA", result!.Address.City);
        Assert.Equal(40.7, result.Address.Coords.Latitude);
    }

    [Fact]
    public void ThreeLevel_EncoderDecoder_AllLevelsChange()
    {
        var initial = MakeUser("Alice", 30, "NYC", 40.7, -74.0);
        var encoder = new DeltaStreamEncoder<MinifiedUserDto>(initial, MinifiedUserContext.Default.MinifiedUserDto);
        var decoder = new DeltaStreamDecoder<MinifiedUserDto>(encoder.MainFrame);

        var updated = MakeUser("Bob", 25, "LA", 34.0, -118.2);
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("Bob", result!.Name);
        Assert.Equal(25, result.Age);
        Assert.Equal("LA", result.Address.City);
        Assert.Equal(34.0, result.Address.Coords.Latitude);
        Assert.Equal(-118.2, result.Address.Coords.Longitude);
    }

    [Fact]
    public void ThreeLevel_MultipleDeltas_AccumulateAcrossAllLevels()
    {
        var initial = MakeUser("Alice", 30, "NYC", 40.7, -74.0);
        var encoder = new DeltaStreamEncoder<MinifiedUserDto>(initial, MinifiedUserContext.Default.MinifiedUserDto);
        var decoder = new DeltaStreamDecoder<MinifiedUserDto>(encoder.MainFrame);

        // Delta 1: change name only
        var r1 = decoder.DecodeFrame(encoder.EncodeChanges(
            MakeUser("Bob", 30, "NYC", 40.7, -74.0)));
        Assert.Equal("Bob", r1!.Name);
        Assert.Equal("NYC", r1.Address.City);

        // Delta 2: change coords only
        var r2 = decoder.DecodeFrame(encoder.EncodeChanges(
            MakeUser("Bob", 30, "NYC", 41.0, -74.0)));
        Assert.Equal("Bob", r2!.Name);
        Assert.Equal(41.0, r2.Address.Coords.Latitude);

        // Delta 3: change city and age
        var r3 = decoder.DecodeFrame(encoder.EncodeChanges(
            MakeUser("Bob", 31, "LA", 41.0, -74.0)));
        Assert.Equal(31, r3!.Age);
        Assert.Equal("LA", r3.Address.City);
        Assert.Equal(41.0, r3.Address.Coords.Latitude);
    }
}
