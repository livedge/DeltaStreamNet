namespace DeltaStreamNet.Tests;

[DeltaStreamSerializable(typeof(TestDto))]
public partial class TestContext : DeltaStreamContext { }

[StreamFrame]
public record TestDto
{
    public required string Name { get; set; }
    public required int Score { get; set; }
}

// --- Nested StreamFrame types ------------------------------------------------

[StreamFrame]
public record NestedDto
{
    public required string Tag   { get; set; }
    public required int    Value { get; set; }
}

[StreamFrame]
public record ParentDto
{
    public required string    Label { get; set; }
    public required NestedDto Child { get; set; }
}

[DeltaStreamSerializable(typeof(ParentDto))]
public partial class ParentContext : DeltaStreamContext { }
