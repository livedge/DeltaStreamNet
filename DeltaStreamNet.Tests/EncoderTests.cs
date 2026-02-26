using Xunit;

namespace DeltaStreamNet.Tests;

public class EncoderTests
{
    [Fact]
    public void Constructor_SetsInitialKeyFrame_AtVersionZero()
    {
        var initial = new TestDto { Name = "Alice", Score = 100 };
        var encoder = new DeltaStreamEncoder<TestDto>(initial, TestContext.Default.TestDto);

        Assert.Equal(0UL, encoder.MainFrame.Version);
        Assert.Equal(initial, encoder.MainFrame.Value);
    }

    [Fact]
    public void EncodeChanges_IncrementsVersionOnDeltaAndMainFrame()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);

        var delta = encoder.EncodeChanges(new TestDto { Name = "Alice", Score = 200 });

        Assert.Equal(1UL, delta.Version);
        Assert.Equal(1UL, encoder.MainFrame.Version);
    }

    [Fact]
    public void EncodeChanges_PatchMarksOnlyChangedProperty()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);

        var delta = encoder.EncodeChanges(new TestDto { Name = "Alice", Score = 200 });
        var patch = (TestDtoDeltaFrame)delta.Patch;

        Assert.Null(patch.Name);
        Assert.NotNull(patch.Score);
        Assert.Equal(200, patch.Score!.Value.Value);
    }

    [Fact]
    public void EncodeChanges_PatchMarksOnlyChangedProperty_OtherField()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);

        var delta = encoder.EncodeChanges(new TestDto { Name = "Bob", Score = 100 });
        var patch = (TestDtoDeltaFrame)delta.Patch;

        Assert.NotNull(patch.Name);
        Assert.Null(patch.Score);
        Assert.Equal("Bob", patch.Name!.Value.Value);
    }

    [Fact]
    public void EncodeChanges_AdvancesMainFrameValue()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);

        var updated = new TestDto { Name = "Bob", Score = 200 };
        encoder.EncodeChanges(updated);

        Assert.Equal(updated, encoder.MainFrame.Value);
    }

    [Fact]
    public void EncodeChanges_DeltaSharesUuidWithMainFrame()
    {
        var encoder = new DeltaStreamEncoder<TestDto>(
            new TestDto { Name = "Alice", Score = 100 },
            TestContext.Default.TestDto);

        var delta = encoder.EncodeChanges(new TestDto { Name = "Bob", Score = 200 });

        Assert.Equal(encoder.MainFrame.EncoderUuid, delta.EncoderUuid);
    }
}
