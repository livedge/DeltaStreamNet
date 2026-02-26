using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeltaStreamNet.Tests;

[StreamFrame(MinifyJson = true)]
public record MinifiedDto
{
    public required string Name { get; set; }
    public required int Score { get; set; }
    public required double Price { get; set; }
    public required bool IsActive { get; set; }
    public required long Id { get; set; }
}

[DeltaStreamSerializable(typeof(MinifiedDto))]
public partial class MinifiedContext : DeltaStreamContext { }

// Collision handling: two props starting with same letter
[StreamFrame(MinifyJson = true)]
public record CollisionDto
{
    public required string Price { get; set; }
    public required string Product { get; set; }
    public required string Name { get; set; }
}

[DeltaStreamSerializable(typeof(CollisionDto))]
public partial class CollisionContext : DeltaStreamContext { }

// Both MinifyJson and PropagateAttributes
[StreamFrame(MinifyJson = true, PropagateAttributes = true)]
public record MinifiedAnnotatedDto
{
    [JsonPropertyName("player_name")]
    [System.ComponentModel.Description("The player's name")]
    public required string Name  { get; set; }

    [JsonPropertyName("player_score")]
    public required int    Score { get; set; }
}

[DeltaStreamSerializable(typeof(MinifiedAnnotatedDto))]
public partial class MinifiedAnnotatedContext : DeltaStreamContext { }

// --- Complex MinifyJson DTOs: nested, keyed collections, multi-level --------

[StreamFrame(MinifyJson = true)]
public record MinifiedInnerDto
{
    public required string Tag   { get; set; }
    public required int    Value { get; set; }
}

[StreamFrame(MinifyJson = true)]
public record MinifiedOuterDto
{
    public required string          Label { get; set; }
    public required MinifiedInnerDto Child { get; set; }
}

[DeltaStreamSerializable(typeof(MinifiedOuterDto))]
public partial class MinifiedOuterContext : DeltaStreamContext { }

// Keyed collection with MinifyJson
[StreamFrame(MinifyJson = true)]
public record MinifiedItemDto
{
    [StreamKey]
    public required string Sku     { get; set; }
    public required decimal Price  { get; set; }
    public required int    Stock   { get; set; }
}

[StreamFrame(MinifyJson = true)]
public record MinifiedCatalogDto
{
    public required string                Title  { get; set; }
    public required List<MinifiedItemDto> Items  { get; set; }
}

[DeltaStreamSerializable(typeof(MinifiedCatalogDto))]
public partial class MinifiedCatalogContext : DeltaStreamContext { }

// Three-level nesting with MinifyJson
[StreamFrame(MinifyJson = true)]
public record MinifiedGeoDto
{
    public required double Latitude  { get; set; }
    public required double Longitude { get; set; }
}

[StreamFrame(MinifyJson = true)]
public record MinifiedAddressDto
{
    public required string        City    { get; set; }
    public required MinifiedGeoDto Coords { get; set; }
}

[StreamFrame(MinifyJson = true)]
public record MinifiedUserDto
{
    public required string             Name    { get; set; }
    public required int                Age     { get; set; }
    public required MinifiedAddressDto Address { get; set; }
}

[DeltaStreamSerializable(typeof(MinifiedUserDto))]
public partial class MinifiedUserContext : DeltaStreamContext { }

// Mixed mode: parent MinifyJson + PropagateAttributes, child MinifyJson only
[StreamFrame(MinifyJson = true, PropagateAttributes = true)]
public record MixedParentDto
{
    [JsonPropertyName("long_label")]
    public required string          Label { get; set; }
    public required MinifiedInnerDto Child { get; set; }
}

[DeltaStreamSerializable(typeof(MixedParentDto))]
public partial class MixedParentContext : DeltaStreamContext { }

// Many properties with collisions: Pa, Pb, Pc, Qx, Qy
[StreamFrame(MinifyJson = true)]
public record ManyCollisionsDto
{
    public required string Padding    { get; set; }
    public required string Password   { get; set; }
    public required string Path       { get; set; }
    public required int    Quantity   { get; set; }
    public required int    Quality    { get; set; }
    public required bool   Active     { get; set; }
}

[DeltaStreamSerializable(typeof(ManyCollisionsDto))]
public partial class ManyCollisionsContext : DeltaStreamContext { }
