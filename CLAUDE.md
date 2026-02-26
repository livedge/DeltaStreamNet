# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run tests
dotnet test DeltaStreamNet.Tests/DeltaStreamNet.Tests.csproj

# Run a single test class
dotnet test DeltaStreamNet.Tests --filter "FullyQualifiedName~EncoderTests"

# Run benchmarks
dotnet run --project DeltaStreamNet.Benchmarks --configuration Release
```

## Architecture

DeltaStreamNet is a .NET library for delta streaming — transmitting only changed properties of objects rather than full state, using Roslyn incremental source generation.

### Project Layout

| Project | Target | Role |
|---------|--------|------|
| `DeltaStreamNet` | net9.0 | Core runtime library (encoders, decoders, interfaces) |
| `DeltaStreamNet.Annotations` | net10.0 | Marker attributes only (`[StreamFrame]`, `[StreamField]`, `[DeltaStreamSerializable]`) |
| `DeltaStreamNet.Generator` | netstandard2.0 | Roslyn `IIncrementalGenerator`; uses Scriban templates |
| `DeltaStreamNet.Tests` | net9.0 | xUnit tests |
| `DeltaStreamNet.Benchmarks` | net9.0 | BenchmarkDotNet perf tests |
| `DeltaStreamNet.Sample` | net9.0 | Real-world usage example |

### Core Types (DeltaStreamNet/)

```
Frame<T>  (record)
├── KeyFrame<T>    — full-state snapshot; contains the complete DTO value
└── DeltaFrame<T>  — incremental change; contains IDeltaPatch<T>
```

- **`DeltaStreamEncoder<T>`**: Accepts successive DTOs, computes patches via `IDeltaFrameGenerator<T>`, emits `KeyFrame` initially then `DeltaFrame`s. Tracks version and UUID.
- **`DeltaStreamDecoder<T>`**: Accepts `KeyFrame` (replaces state) or `DeltaFrame` (applies patch). Validates UUID matching and version monotonicity.
- **`IDeltaPatch<T>`**: Single method `ApplyPatch(T value) → T`.
- **`IDeltaFrameGenerator<T>`**: Single method `GeneratePatch(T previous, T current) → IDeltaPatch<T>`.
- **`DeltaStreamContext`**: Abstract base class for the generated context that holds generators.

### Source Generator Pipelines (DeltaStreamNet.Generator/)

The generator runs two pipelines triggered by different attributes:

**Pipeline 1 — `[StreamFrame]` on a record:**
Generates three files per type:
- `{Name}KeyFrame.g.cs` — mutable class wrapper with `From()` / `ToDto()` converters
- `{Name}DeltaFrame.g.cs` — implements `IDeltaPatch<T>`; uses `PropertyDeltaWrapper<T>?` for scalars, nested nullable delta frames for `[StreamFrame]`-decorated property types
- `{Name}DeltaFrameGenerator.g.cs` — implements `IDeltaFrameGenerator<T>`; wraps `{Name}DeltaFrame.Create(previous, current)`

**Pipeline 2 — `[DeltaStreamSerializable(typeof(T))]` on a partial class:**
Generates a partial class extending `DeltaStreamContext` with typed generator properties and a `Default` singleton.

Templates are Scriban `.sbncs` files embedded as resources and loaded by `TemplateLoader`.

### Key Patterns

- **`PropertyDeltaWrapper<T>?`**: A nullable `record struct` with a `Value` field used in generated delta frames. `null` means unchanged (omitted from JSON); non-null means changed.
- **`FrameType` enum**: `Key = 0`, `Delta = 1` — carried as `"f"` on every `Frame<T>` for wire-level type discrimination.
- **Nested delta recursion**: If a DTO property is itself a `[StreamFrame]` record, the generator uses a nested delta frame instead of a wrapper, enabling efficient deep updates.
- **Dual representation**: Users work with immutable records (DTOs); generated `KeyFrame` classes are mutable for efficient comparison during patch generation.
- All projects use implicit usings and nullable reference types enabled by default.
