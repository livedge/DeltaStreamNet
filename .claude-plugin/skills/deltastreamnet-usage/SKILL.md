---
name: deltastreamnet-usage
description: >-
  Use when code references DeltaStreamNet, StreamFrame, StreamField, StreamKey,
  DeltaStreamSerializable, DeltaStreamEncoder, DeltaStreamDecoder, DeltaStreamContext,
  Frame<T>, KeyFrame, DeltaFrame, PropertyDeltaWrapper, FrameProtobufRegistry,
  FrameJsonConverterFactory, or when the user asks about delta streaming, delta encoding,
  or transmitting only changed properties. Also trigger when a project has a PackageReference
  to DeltaStreamNet or DeltaStreamNet.Protobuf.
---

# DeltaStreamNet — Delta Streaming Library

DeltaStreamNet transmits only changed properties of .NET records over the wire using Roslyn source generation. **Do NOT decompile, disassemble, or inspect the NuGet package internals. Everything you need is here.**

## Packages

| Package | Purpose |
|---------|---------|
| `DeltaStreamNet` | Core library + source generator + annotations (all-in-one) |
| `DeltaStreamNet.Protobuf` | Protobuf serialization support via `FrameProtobufRegistry` |

## Step-by-step usage

### 1. Define a DTO record with `[StreamFrame]`

```csharp
using DeltaStreamNet;

[StreamFrame]
public record PriceDto
{
    public required string Symbol { get; set; }
    public required decimal Price { get; set; }
    public required decimal Bid { get; set; }
    public required decimal Ask { get; set; }
    public required long Volume { get; set; }
}
```

The source generator automatically creates:
- `PriceDtoKeyFrame` — mutable snapshot class with `From()`/`ToDto()`
- `PriceDtoDeltaFrame` — patch class implementing `IDeltaPatch<PriceDto>`
- `PriceDtoDeltaFrameGenerator` — implements `IDeltaFrameGenerator<PriceDto>`

**Do NOT create these classes manually. They are source-generated.**

### 2. Create a serialization context

```csharp
[DeltaStreamSerializable(typeof(PriceDto))]
public partial class TradingContext : DeltaStreamContext { }
```

One context can register multiple DTOs:

```csharp
[DeltaStreamSerializable(typeof(PriceDto))]
[DeltaStreamSerializable(typeof(OrderDto))]
[DeltaStreamSerializable(typeof(TradeDto))]
public partial class TradingContext : DeltaStreamContext { }
```

The generator creates a `Default` singleton and typed generator properties (e.g. `TradingContext.Default.PriceDto`).

### 3. Encode (sender side)

```csharp
// First state — produces a KeyFrame (full snapshot)
var encoder = new DeltaStreamEncoder<PriceDto>(initialDto, TradingContext.Default.PriceDto);
Frame<PriceDto> frame = encoder.MainFrame;
Send(frame); // serialize and send

// Subsequent updates — produces DeltaFrames (only changed properties)
Frame<PriceDto> delta = encoder.EncodeChanges(updatedDto);
Send(delta);
```

### 4. Decode (receiver side)

```csharp
var decoder = new DeltaStreamDecoder<PriceDto>();

// Works for both KeyFrame and DeltaFrame
PriceDto current = decoder.ApplyFrame(receivedFrame);
```

The decoder validates:
- UUID matching (rejects frames from a different encoder)
- Version monotonicity (rejects out-of-order frames)

### 5. Serialize `Frame<T>` for the wire

**JSON (System.Text.Json):**

```csharp
// Serialization — works out of the box
var json = JsonSerializer.Serialize(frame);

// Deserialization — needs the converter factory
var options = new JsonSerializerOptions();
options.Converters.Add(new FrameJsonConverterFactory()); // registers Frame<T> polymorphism
var back = JsonSerializer.Deserialize<Frame<PriceDto>>(json, options);
```

**Protobuf (requires DeltaStreamNet.Protobuf package):**

```csharp
// Registration happens automatically when context is accessed
_ = TradingContext.Default;

Serializer.Serialize(stream, frame);
var back = Serializer.Deserialize<Frame<PriceDto>>(stream);
```

## StreamFrame options

### Attribute propagation

Copies attributes from the DTO onto generated types (useful for JSON/Protobuf annotations):

```csharp
[StreamFrame(PropagateAttributes = true)]
public record PriceDto
{
    [JsonPropertyName("sym")]
    public required string Symbol { get; set; }

    [JsonPropertyName("px")]
    public required decimal Price { get; set; }
}
```

### Minified JSON

Auto-generates short `[JsonPropertyName]` values for compact wire format:

```csharp
[StreamFrame(MinifyJson = true)]
public record PriceDto { ... }
```

Can combine with `PropagateAttributes = true` — explicit `[JsonPropertyName]` on the DTO takes precedence, others get auto-minified.

### Protobuf support

When the DTO has `[ProtoContract]`, the generator emits `[ProtoContract]`/`[ProtoMember]` on generated KeyFrame/DeltaFrame types:

```csharp
[ProtoContract]
[StreamFrame]
public record PriceDto
{
    [ProtoMember(1)]
    public required string Symbol { get; set; }

    [ProtoMember(2)]
    public required decimal Price { get; set; }
}
```

If `PropagateAttributes = true` is also set, the generator skips emitting protobuf attrs to avoid duplication (they're already propagated).

`FrameProtobufRegistry` handles `Frame<T>` hierarchy polymorphism at runtime — this is called automatically by the generated context constructor.

### Nested DTOs

If a property type is also a `[StreamFrame]` record, the generator produces nested delta frames for efficient deep updates:

```csharp
[StreamFrame]
public record AddressDto
{
    public required string City { get; set; }
    public required string Zip { get; set; }
}

[StreamFrame]
public record PersonDto
{
    public required string Name { get; set; }
    public required AddressDto Address { get; set; } // nested delta
}
```

### Keyed collections

Use `[StreamKey]` on a property to enable list diffing (additions, modifications, deletions, reordering):

```csharp
[StreamFrame]
public record RunnerDto
{
    [StreamKey]
    public required string Id { get; set; }
    public required decimal Odds { get; set; }
    public required string Status { get; set; }
}

[StreamFrame]
public record MarketDto
{
    public required string Name { get; set; }
    public required List<RunnerDto> Runners { get; set; } // keyed collection delta
}
```

## Frame<T> type hierarchy

```
Frame<T>  (abstract record — the wire type)
├── KeyFrame<T>    — full-state snapshot (Value property)
└── DeltaFrame<T>  — incremental change (Patch property: IDeltaPatch<T>)
```

Frame carries: `Type` (Key/Delta enum), `EncoderUuid`, `Version`, `Timestamp`.

## Common mistakes to avoid

- **Do NOT manually create** KeyFrame/DeltaFrame/DeltaFrameGenerator classes — they are source-generated
- **Do NOT inspect** the DeltaStreamNet NuGet package internals — use this reference instead
- **Do NOT forget** `FrameJsonConverterFactory` when deserializing `Frame<T>` from JSON
- **Do NOT construct** `KeyFrame<T>` or `DeltaFrame<T>` directly — use `DeltaStreamEncoder`
- **The context class must be `partial`** — the generator extends it
- **DTO must be a `record`** (not a class) with `{ get; set; }` properties
