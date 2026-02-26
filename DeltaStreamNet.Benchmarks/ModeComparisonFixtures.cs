using System.Text.Json.Serialization;

namespace DeltaStreamNet.Benchmarks;

// Mode 1: Plain (no flags)
[StreamFrame]
public record PlainDto
{
    public required string Name { get; set; }
    public required int Score { get; set; }
    public required double Price { get; set; }
    public required bool IsActive { get; set; }
    public required long Id { get; set; }
}

[DeltaStreamSerializable(typeof(PlainDto))]
public partial class PlainContext : DeltaStreamContext { }

// Mode 2: PropagateAttributes only (user-chosen JSON names)
[StreamFrame(PropagateAttributes = true)]
public record PropagateDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("score")]
    public required int Score { get; set; }

    [JsonPropertyName("price")]
    public required double Price { get; set; }

    [JsonPropertyName("is_active")]
    public required bool IsActive { get; set; }

    [JsonPropertyName("id")]
    public required long Id { get; set; }
}

[DeltaStreamSerializable(typeof(PropagateDto))]
public partial class PropagateContext : DeltaStreamContext { }

// Mode 3: MinifyJson only (auto short names)
[StreamFrame(MinifyJson = true)]
public record MinifyDto
{
    public required string Name { get; set; }
    public required int Score { get; set; }
    public required double Price { get; set; }
    public required bool IsActive { get; set; }
    public required long Id { get; set; }
}

[DeltaStreamSerializable(typeof(MinifyDto))]
public partial class MinifyContext : DeltaStreamContext { }

// Mode 4: Both (PayloadDto in PayloadDto.cs already covers this)
