using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeltaStreamNet.Tests;

public class MinifyJsonKeyedCollectionTests
{
    private static MinifiedCatalogDto MakeCatalog(string title,
        params (string sku, decimal price, int stock)[] items) =>
        new()
        {
            Title = title,
            Items = items.Select(i => new MinifiedItemDto
                { Sku = i.sku, Price = i.price, Stock = i.stock }).ToList()
        };

    [Fact]
    public void CatalogKeyFrame_HasMinifiedNames()
    {
        // Title→"t", Items→"i"
        var titleProp = typeof(MinifiedCatalogDtoKeyFrame).GetProperty("Title")!;
        var titleAttr = titleProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("t", titleAttr.Name);

        var itemsProp = typeof(MinifiedCatalogDtoKeyFrame).GetProperty("Items")!;
        var itemsAttr = itemsProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("i", itemsAttr.Name);
    }

    [Fact]
    public void ItemKeyFrame_HasMinifiedNames()
    {
        // Sku→"sk" (collision with Stock→"st"), Price→"p"
        var skuProp = typeof(MinifiedItemDtoKeyFrame).GetProperty("Sku")!;
        var skuAttr = skuProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        var stockProp = typeof(MinifiedItemDtoKeyFrame).GetProperty("Stock")!;
        var stockAttr = stockProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();

        // Sku and Stock both start with 's', so they get longer prefixes
        Assert.NotEqual(skuAttr.Name, stockAttr.Name);

        var priceProp = typeof(MinifiedItemDtoKeyFrame).GetProperty("Price")!;
        var priceAttr = priceProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("p", priceAttr.Name);
    }

    [Fact]
    public void Catalog_EncoderDecoder_ItemModified()
    {
        var initial = MakeCatalog("Shop", ("A", 10m, 5), ("B", 20m, 10));
        var encoder = new DeltaStreamEncoder<MinifiedCatalogDto>(initial, MinifiedCatalogContext.Default.MinifiedCatalogDto);
        var decoder = new DeltaStreamDecoder<MinifiedCatalogDto>(encoder.MainFrame);

        var updated = MakeCatalog("Shop", ("A", 15m, 5), ("B", 20m, 10));
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("Shop", result!.Title);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(15m, result.Items.First(x => x.Sku == "A").Price);
        Assert.Equal(20m, result.Items.First(x => x.Sku == "B").Price);
    }

    [Fact]
    public void Catalog_EncoderDecoder_ItemAddedAndRemoved()
    {
        var initial = MakeCatalog("Shop", ("A", 10m, 5), ("B", 20m, 10));
        var encoder = new DeltaStreamEncoder<MinifiedCatalogDto>(initial, MinifiedCatalogContext.Default.MinifiedCatalogDto);
        var decoder = new DeltaStreamDecoder<MinifiedCatalogDto>(encoder.MainFrame);

        // Remove B, add C
        var updated = MakeCatalog("Shop", ("A", 10m, 5), ("C", 30m, 15));
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal(2, result!.Items.Count);
        Assert.Contains(result.Items, x => x.Sku == "A");
        Assert.Contains(result.Items, x => x.Sku == "C");
        Assert.DoesNotContain(result.Items, x => x.Sku == "B");
    }

    [Fact]
    public void Catalog_TitleAndItemsBothChange()
    {
        var initial = MakeCatalog("Shop v1", ("A", 10m, 5));
        var encoder = new DeltaStreamEncoder<MinifiedCatalogDto>(initial, MinifiedCatalogContext.Default.MinifiedCatalogDto);
        var decoder = new DeltaStreamDecoder<MinifiedCatalogDto>(encoder.MainFrame);

        var updated = MakeCatalog("Shop v2", ("A", 25m, 5));
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal("Shop v2", result!.Title);
        Assert.Equal(25m, result.Items.Single().Price);
    }

    [Fact]
    public void Catalog_CollectionDelta_HasJsonIgnoreOnHasChanges()
    {
        var hasChangesProp = typeof(MinifiedItemDtoCollectionKeyedDelta).GetProperty("HasChanges")!;
        var ignoreAttr = hasChangesProp.GetCustomAttributes(typeof(JsonIgnoreAttribute), false);
        Assert.Single(ignoreAttr);
    }

    [Fact]
    public void Catalog_CollectionDelta_HasMinifiedPropertyNames()
    {
        // Modifications→"m", Additions→"a", Deletions→"d", Order→"o"
        var modsProp = typeof(MinifiedItemDtoCollectionKeyedDelta).GetProperty("Modifications")!;
        var modsAttr = modsProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("m", modsAttr.Name);

        var addsProp = typeof(MinifiedItemDtoCollectionKeyedDelta).GetProperty("Additions")!;
        var addsAttr = addsProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("a", addsAttr.Name);

        var delsProp = typeof(MinifiedItemDtoCollectionKeyedDelta).GetProperty("Deletions")!;
        var delsAttr = delsProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("d", delsAttr.Name);

        var orderProp = typeof(MinifiedItemDtoCollectionKeyedDelta).GetProperty("Order")!;
        var orderAttr = orderProp.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .Cast<JsonPropertyNameAttribute>().Single();
        Assert.Equal("o", orderAttr.Name);
    }

    [Fact]
    public void Catalog_MultipleDeltas_AccumulateCorrectly()
    {
        var initial = MakeCatalog("Shop", ("A", 10m, 5), ("B", 20m, 10));
        var encoder = new DeltaStreamEncoder<MinifiedCatalogDto>(initial, MinifiedCatalogContext.Default.MinifiedCatalogDto);
        var decoder = new DeltaStreamDecoder<MinifiedCatalogDto>(encoder.MainFrame);

        // Delta 1: modify A price
        var r1 = decoder.DecodeFrame(encoder.EncodeChanges(
            MakeCatalog("Shop", ("A", 12m, 5), ("B", 20m, 10))));
        Assert.Equal(12m, r1!.Items.First(x => x.Sku == "A").Price);

        // Delta 2: add C
        var r2 = decoder.DecodeFrame(encoder.EncodeChanges(
            MakeCatalog("Shop", ("A", 12m, 5), ("B", 20m, 10), ("C", 30m, 1))));
        Assert.Equal(3, r2!.Items.Count);

        // Delta 3: remove B, change title
        var r3 = decoder.DecodeFrame(encoder.EncodeChanges(
            MakeCatalog("Shop v2", ("A", 12m, 5), ("C", 30m, 1))));
        Assert.Equal("Shop v2", r3!.Title);
        Assert.Equal(2, r3.Items.Count);
        Assert.DoesNotContain(r3.Items, x => x.Sku == "B");
    }
}
