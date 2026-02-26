# DeltaStreamNet

[![CI](https://github.com/livedge/DeltaStreamNet/actions/workflows/ci.yml/badge.svg)](https://github.com/livedge/DeltaStreamNet/actions/workflows/ci.yml)
[![GitHub Package](https://img.shields.io/badge/nuget-GitHub%20Packages-blue?logo=nuget)](https://github.com/livedge/DeltaStreamNet/pkgs/nuget/DeltaStreamNet)

A .NET library for delta streaming — transmit only changed properties instead of full object state, powered by Roslyn incremental source generation. Zero reflection, fully compile-time.

## Quick Start

### 1. Define your DTO

Annotate any record with `[StreamFrame]`:

```csharp
[StreamFrame]
public record Product
{
    public required string Name { get; set; }
    public required decimal Price { get; set; }
    public required int Stock { get; set; }
}
```

### 2. Create a context

```csharp
[DeltaStreamSerializable(typeof(Product))]
public partial class AppContext : DeltaStreamContext { }
```

### 3. Encode

```csharp
var encoder = new DeltaStreamEncoder<Product>(
    new Product { Name = "Widget", Price = 9.99m, Stock = 50 },
    AppContext.Default.Product
);

// First frame is always a full-state KeyFrame
KeyFrame<Product> keyFrame = encoder.MainFrame;

// Subsequent calls produce DeltaFrames containing only what changed
DeltaFrame<Product> delta = encoder.EncodeChanges(
    new Product { Name = "Widget", Price = 12.99m, Stock = 50 }
);
// delta contains only the Price change — Name and Stock are omitted
```

### 4. Decode

```csharp
var decoder = new DeltaStreamDecoder<Product>(keyFrame);

Product updated = decoder.DecodeFrame(delta);
// updated.Price == 12.99m
```

### 5. Serialize

Frames serialize directly with `System.Text.Json`:

```json
// KeyFrame — full state
{ "f": 0, "u": "...", "v": 0, "t": "...", "d": { "Name": "Widget", "Price": 9.99, "Stock": 50 } }

// DeltaFrame — only changes
{ "f": 1, "u": "...", "v": 1, "t": "...", "p": { "Price": { "v": 12.99 } } }
```

Unchanged properties are `null` and omitted via `JsonIgnoreCondition.WhenWritingNull`.

## Features

### Nested Delta Recursion

When a property type is also a `[StreamFrame]`, changes propagate recursively — only the leaf properties that changed are transmitted:

```csharp
[StreamFrame]
public record Order
{
    public required string Id { get; set; }
    public required Address Shipping { get; set; }
}

[StreamFrame]
public record Address
{
    public required string City { get; set; }
    public required string Zip { get; set; }
}
```

Changing only `order.Shipping.Zip` produces a delta with just the nested zip change.

### Keyed Collections

Use `[StreamKey]` on list item records to get per-element diffing with tracked additions, modifications, deletions, and ordering:

```csharp
[StreamFrame]
public record Runner
{
    [StreamKey]
    public required string Name { get; set; }
    public required decimal Price { get; set; }
}

[StreamFrame]
public record Market
{
    public required List<Runner> Runners { get; set; }
}
```

The generated `RunnerCollectionKeyedDelta` tracks:
- **Modifications** — per-item deltas (only changed fields within each item)
- **Additions** — new items
- **Deletions** — removed item keys
- **Order** — current key sequence

### JSON Minification

Auto-shorten JSON property names for smaller payloads:

```csharp
[StreamFrame(MinifyJson = true)]
public record Ticker
{
    public required string Symbol { get; set; }   // -> "s"
    public required decimal Price { get; set; }    // -> "p"
    public required int Volume { get; set; }       // -> "v"
}
```

Uses a prefix minimization algorithm — finds the shortest unique prefix for each property name.

### Attribute Propagation

Forward property-level attributes (e.g., `[JsonPropertyName]`, `[ProtoMember]`) to generated code:

```csharp
[StreamFrame(PropagateAttributes = true)]
public record Quote
{
    [JsonPropertyName("px")]
    public required decimal Price { get; set; }

    [JsonPropertyName("qty")]
    public required int Quantity { get; set; }
}
```

Both `PropagateAttributes` and `MinifyJson` can be combined — when both are set, `[JsonPropertyName]` is replaced by the auto-minified name while other attributes are preserved.

## Reliability

Every frame carries metadata for robust streaming:

| Field | Wire | Purpose |
|-------|------|---------|
| `FrameType` | `"f"` | `Key` (0) or `Delta` (1) — frame type discrimination |
| `EncoderUuid` | `"u"` | Identifies the producer — decoder rejects mismatched UUIDs |
| `Version` | `"v"` | Monotonically increasing — decoder validates sequential ordering |
| `Timestamp` | `"t"` | Transmission timestamp |

If a consumer detects a version gap, it can request a new KeyFrame to resync.

## How It Works

The source generator produces three classes per `[StreamFrame]` record at compile time:

| Generated Class | Role |
|----------------|------|
| `{Name}KeyFrame` | Mutable class with `From(dto)` / `ToDto()` converters for efficient comparison |
| `{Name}DeltaFrame` | Implements `IDeltaPatch<T>` — uses nullable `PropertyDeltaWrapper<T>?` per property (`null` = unchanged) |
| `{Name}DeltaFrameGenerator` | Implements `IDeltaFrameGenerator<T>` — compares two KeyFrames to produce a DeltaFrame |

A `[DeltaStreamSerializable]` context generates a partial class with typed generator properties and a `Default` singleton.

## Project Structure

| Project | Target | Role |
|---------|--------|------|
| `DeltaStreamNet` | net10.0 | Core runtime — encoders, decoders, frame types, interfaces |
| `DeltaStreamNet.Annotations` | net10.0 | Attributes — `[StreamFrame]`, `[StreamField]`, `[StreamKey]`, `[DeltaStreamSerializable]` |
| `DeltaStreamNet.Generator` | netstandard2.0 | Roslyn `IIncrementalGenerator` with Scriban templates |
| `DeltaStreamNet.Tests` | net9.0 | xUnit tests |
| `DeltaStreamNet.Benchmarks` | net9.0 | BenchmarkDotNet performance tests |
| `DeltaStreamNet.Sample` | net10.0 | Real-world sports market data example |

## Building

```bash
dotnet build
dotnet test DeltaStreamNet.Tests/DeltaStreamNet.Tests.csproj
dotnet run --project DeltaStreamNet.Benchmarks --configuration Release
```

## License

[MIT](LICENSE)
