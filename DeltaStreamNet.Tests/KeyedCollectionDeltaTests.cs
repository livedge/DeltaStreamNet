using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DeltaStreamNet.Tests;

public class KeyedCollectionDeltaTests
{
    private static List<MarketItemDto> MakeItems(params (string id, decimal price, int vol)[] items) =>
        items.Select(i => new MarketItemDto { Id = i.id, Price = i.price, Volume = i.vol }).ToList();

    private static MarketBoardDto MakeBoard(params (string id, decimal price, int vol)[] items) =>
        new MarketBoardDto { Name = "TestBoard", Items = MakeItems(items) };

    [Fact]
    public void Create_ModifiedElement_ProducesModificationEntry()
    {
        var prev = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));
        var curr = MakeItems(("A", 1.5m, 100), ("B", 2.0m, 200));

        var delta = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);

        Assert.Single(delta.Modifications);
        Assert.Equal("A", delta.Modifications[0].Key);
        Assert.Empty(delta.Additions);
        Assert.Empty(delta.Deletions);
    }

    [Fact]
    public void Create_NewElement_ProducesAdditionEntry()
    {
        var prev = MakeItems(("A", 1.0m, 100));
        var curr = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));

        var delta = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);

        Assert.Empty(delta.Modifications);
        Assert.Single(delta.Additions);
        Assert.Equal("B", delta.Additions[0].Id);
        Assert.Empty(delta.Deletions);
    }

    [Fact]
    public void Create_RemovedElement_ProducesDeletionEntry()
    {
        var prev = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));
        var curr = MakeItems(("A", 1.0m, 100));

        var delta = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);

        Assert.Empty(delta.Modifications);
        Assert.Empty(delta.Additions);
        Assert.Single(delta.Deletions);
        Assert.Equal("B", delta.Deletions[0]);
    }

    [Fact]
    public void Create_NoChanges_HasChangesIsFalse()
    {
        var prev = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));
        var curr = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));

        var delta = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);

        Assert.False(delta.HasChanges);
    }

    [Fact]
    public void ApplyPatch_ModifiedElement_UpdatesCorrectElement()
    {
        var prev = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));
        var curr = MakeItems(("A", 1.5m, 100), ("B", 2.0m, 200));

        var delta  = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);
        var result = delta.ApplyPatch(prev);

        Assert.Equal(2, result.Count);
        Assert.Equal(1.5m, result.First(x => x.Id == "A").Price);
        Assert.Equal(2.0m, result.First(x => x.Id == "B").Price);
    }

    [Fact]
    public void ApplyPatch_PreservesOrder_MatchingCurrentOrder()
    {
        var prev = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));
        var curr = MakeItems(("B", 2.0m, 200), ("A", 1.0m, 100));  // reversed

        var delta  = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);
        var result = delta.ApplyPatch(prev);

        Assert.Equal("B", result[0].Id);
        Assert.Equal("A", result[1].Id);
    }

    [Fact]
    public void ApplyPatch_AdditionAndDeletion_ProducesCorrectList()
    {
        var prev = MakeItems(("A", 1.0m, 100), ("B", 2.0m, 200));
        var curr = MakeItems(("A", 1.0m, 100), ("C", 3.0m, 300));  // B removed, C added

        var delta  = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);
        var result = delta.ApplyPatch(prev);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Id == "A");
        Assert.Contains(result, x => x.Id == "C");
        Assert.DoesNotContain(result, x => x.Id == "B");
    }

    [Fact]
    public void ApplyPatch_NullPreviousList_TreatedAsEmpty()
    {
        var curr = MakeItems(("A", 1.0m, 100));

        var delta  = MarketItemDtoCollectionKeyedDelta.Create(null!, curr);
        var result = delta.ApplyPatch(null!);

        Assert.Single(result);
        Assert.Equal("A", result[0].Id);
    }

    [Fact]
    public void ParentDeltaFrame_HasChanges_TrueWhenCollectionChanges()
    {
        var prev = MakeBoard(("A", 1.0m, 100));
        var curr = MakeBoard(("A", 2.0m, 100));

        var encoder = new DeltaStreamEncoder<MarketBoardDto>(prev, MarketBoardContext.Default.MarketBoardDto);
        var delta = encoder.EncodeChanges(curr);
        var patch = (MarketBoardDtoDeltaFrame)delta.Patch;

        Assert.True(patch.HasChanges);
        Assert.NotNull(patch.Items);
        Assert.True(patch.Items!.HasChanges);
    }

    [Fact]
    public void RoundTrip_CollectionChanges_DecoderMatchesCurrent()
    {
        var initial = MakeBoard(("A", 1.0m, 100), ("B", 2.0m, 200));
        var encoder = new DeltaStreamEncoder<MarketBoardDto>(initial, MarketBoardContext.Default.MarketBoardDto);
        var decoder = new DeltaStreamDecoder<MarketBoardDto>(encoder.MainFrame);

        // A modified, B removed, C added
        var updated = MakeBoard(("A", 1.5m, 100), ("C", 3.0m, 300));
        var delta  = encoder.EncodeChanges(updated);
        var result = decoder.DecodeFrame(delta);

        Assert.Equal(2, result!.Items.Count);
        Assert.Equal(1.5m, result.Items.First(x => x.Id == "A").Price);
        Assert.Contains(result.Items, x => x.Id == "C");
        Assert.DoesNotContain(result.Items, x => x.Id == "B");
    }

    [Fact]
    public void ModifiedElement_OnlyChangedFieldsInDelta()
    {
        var prev = MakeItems(("A", 1.0m, 100));
        var curr = MakeItems(("A", 1.5m, 100));  // only Price changed, Volume unchanged

        var delta = MarketItemDtoCollectionKeyedDelta.Create(prev, curr);

        Assert.Single(delta.Modifications);
        var elementDelta = delta.Modifications[0].Delta;
        Assert.NotNull(elementDelta.Price);
        Assert.Null(elementDelta.Volume);
    }
}
