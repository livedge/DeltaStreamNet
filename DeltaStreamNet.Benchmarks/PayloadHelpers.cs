using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProtoBuf.Meta;

namespace DeltaStreamNet.Benchmarks;

public static class PayloadHelpers
{
    public static readonly PayloadDto V1 = new()
        { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 };

    public static readonly PayloadDto V2AllChanged = new()
        { Name = "Bob", Score = 200, Price = 19.99, IsActive = false, Id = 2 };

    public static readonly PayloadDto V2OneChanged = new()
        { Name = "Alice", Score = 200, Price = 9.99, IsActive = true, Id = 1 };

    public static readonly PayloadDto V2NoneChanged = new()
        { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 };

    public static RuntimeTypeModel BuildProtoModel()
    {
        // With [ProtoContract] + [ProtoMember] propagated to both KeyFrame and DeltaFrame,
        // protobuf-net auto-discovers everything. No manual registration needed.
        var model = RuntimeTypeModel.Create();
        model.Add(typeof(PayloadDtoKeyFrame), true);
        model.Add(typeof(PayloadDtoDeltaFrame), true);
        return model;
    }

    /// <summary>
    /// Prints example JSON for KeyFrame and DeltaFrame payloads.
    /// Can be invoked with: dotnet run -- --json-demo
    /// </summary>
    public static void PrintJsonDemo()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var encoder = new DeltaStreamEncoder<PayloadDto>(V1, PayloadContext.Default.PayloadDto);

        // Helper: build a wire-ready object using the minified envelope keys
        // and casting Patch to its concrete type (IDeltaPatch<T> won't serialize polymorphically)
        object WireFrame(DeltaFrame<PayloadDto> delta) => new
        {
            f = delta.Type,
            u = delta.EncoderUuid,
            v = delta.Version,
            t = delta.Timestamp,
            p = (PayloadDtoDeltaFrame)delta.Patch
        };

        Console.WriteLine("========== 1. KeyFrame (initial full state) ==========");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            f = encoder.MainFrame.Type,
            u = encoder.MainFrame.EncoderUuid,
            v = encoder.MainFrame.Version,
            t = encoder.MainFrame.Timestamp,
            d = PayloadDtoKeyFrame.From(V1)
        }, opts));

        Console.WriteLine();
        Console.WriteLine("========== 2. DeltaFrame (Score: 100 → 200, 1 of 5 changed) ==========");
        var delta1 = encoder.EncodeChanges(V2OneChanged);
        Console.WriteLine(JsonSerializer.Serialize(WireFrame(delta1), opts));

        Console.WriteLine();
        Console.WriteLine("========== 3. DeltaFrame (all 5 changed) ==========");
        var delta2 = encoder.EncodeChanges(V2AllChanged);
        Console.WriteLine(JsonSerializer.Serialize(WireFrame(delta2), opts));

        Console.WriteLine();
        Console.WriteLine("========== 4. DeltaFrame (none changed) ==========");
        var delta3 = encoder.EncodeChanges(V2AllChanged);
        Console.WriteLine(JsonSerializer.Serialize(WireFrame(delta3), opts));
    }

    /// <summary>
    /// Prints a payload size comparison table (JSON vs Protobuf) to the console.
    /// Can be invoked directly with: dotnet run -- --payload-report
    /// </summary>
    public static void PrintPayloadSizeReport()
    {
        var protoModel = BuildProtoModel();
        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        var keyFrame = PayloadDtoKeyFrame.From(V1);
        var kfPrev = PayloadDtoKeyFrame.From(V1);
        var deltaAllChanged = PayloadDtoDeltaFrame.Create(kfPrev, PayloadDtoKeyFrame.From(V2AllChanged));
        var deltaOneChanged = PayloadDtoDeltaFrame.Create(kfPrev, PayloadDtoKeyFrame.From(V2OneChanged));
        var deltaNoneChanged = PayloadDtoDeltaFrame.Create(kfPrev, PayloadDtoKeyFrame.From(V2NoneChanged));

        Console.WriteLine();
        Console.WriteLine("=== Payload Size Report: JSON vs Protobuf ===");
        Console.WriteLine();
        Console.WriteLine(string.Format("{0,-35} {1,12} {2,16} {3,10}",
            "Scenario", "JSON (bytes)", "Proto (bytes)", "Savings"));
        Console.WriteLine(new string('-', 77));

        PrintRow("Full DTO (no delta)", V1, keyFrame, protoModel, jsonOptions);
        PrintRow("KeyFrame (full state)", keyFrame, keyFrame, protoModel, jsonOptions);
        PrintDeltaRow("Delta (all 5 changed)", deltaAllChanged, protoModel, jsonOptions);
        PrintDeltaRow("Delta (1 of 5 changed)", deltaOneChanged, protoModel, jsonOptions);
        PrintDeltaRow("Delta (none changed)", deltaNoneChanged, protoModel, jsonOptions);

        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  - JSON uses System.Text.Json with WhenWritingNull");
        Console.WriteLine("  - Protobuf uses protobuf-net with RuntimeTypeModel");
        Console.WriteLine("  - Delta frames use PropertyDeltaWrapper<T>? (null = unchanged)");
        Console.WriteLine("  - Protobuf naturally omits default values (0, false, null)");
        Console.WriteLine();
    }

    private static void PrintRow(string label, object jsonObj, object protoObj,
        RuntimeTypeModel protoModel, JsonSerializerOptions jsonOptions)
    {
        var jsonSize = JsonSerializer.SerializeToUtf8Bytes(jsonObj, jsonOptions).Length;
        using var ms = new MemoryStream();
        protoModel.Serialize(ms, protoObj);
        var protoSize = (int)ms.Length;

        var savings = 1.0 - (double)protoSize / jsonSize;
        Console.WriteLine(string.Format("{0,-35} {1,12} {2,16} {3,9:P1}",
            label, jsonSize, protoSize, savings));
    }

    private static void PrintDeltaRow(string label, PayloadDtoDeltaFrame delta,
        RuntimeTypeModel protoModel, JsonSerializerOptions jsonOptions)
    {
        var jsonSize = JsonSerializer.SerializeToUtf8Bytes(delta, jsonOptions).Length;
        using var ms = new MemoryStream();
        protoModel.Serialize(ms, delta);
        var protoSize = (int)ms.Length;

        var savings = 1.0 - (double)protoSize / jsonSize;
        Console.WriteLine(string.Format("{0,-35} {1,12} {2,16} {3,9:P1}",
            label, jsonSize, protoSize, savings));
    }

    /// <summary>
    /// Prints a comparison of all four StreamFrame modes.
    /// Invoked with: dotnet run -- --mode-comparison
    /// </summary>
    public static void PrintModeComparison()
    {
        var jsonNull    = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        // --- Build data for each mode ---

        // Plain (PropertyDeltaWrapper mode, full property names)
        var plainPrev = PlainDtoKeyFrame.From(new PlainDto { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var plainCurrOne = PlainDtoKeyFrame.From(new PlainDto { Name = "Alice", Score = 200, Price = 9.99, IsActive = true, Id = 1 });
        var plainCurrAll = PlainDtoKeyFrame.From(new PlainDto { Name = "Bob", Score = 200, Price = 19.99, IsActive = false, Id = 2 });
        var plainCurrNone = PlainDtoKeyFrame.From(new PlainDto { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var plainDeltaOne  = PlainDtoDeltaFrame.Create(plainPrev, plainCurrOne);
        var plainDeltaAll  = PlainDtoDeltaFrame.Create(plainPrev, plainCurrAll);
        var plainDeltaNone = PlainDtoDeltaFrame.Create(plainPrev, plainCurrNone);

        // Propagate (nullable mode, user JsonPropertyName: "name"/"score"/etc.)
        var propPrev = PropagateDtoKeyFrame.From(new PropagateDto { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var propCurrOne = PropagateDtoKeyFrame.From(new PropagateDto { Name = "Alice", Score = 200, Price = 9.99, IsActive = true, Id = 1 });
        var propCurrAll = PropagateDtoKeyFrame.From(new PropagateDto { Name = "Bob", Score = 200, Price = 19.99, IsActive = false, Id = 2 });
        var propCurrNone = PropagateDtoKeyFrame.From(new PropagateDto { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var propDeltaOne  = PropagateDtoDeltaFrame.Create(propPrev, propCurrOne);
        var propDeltaAll  = PropagateDtoDeltaFrame.Create(propPrev, propCurrAll);
        var propDeltaNone = PropagateDtoDeltaFrame.Create(propPrev, propCurrNone);

        // MinifyJson (PropertyDeltaWrapper mode, auto short names)
        var minPrev = MinifyDtoKeyFrame.From(new MinifyDto { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var minCurrOne = MinifyDtoKeyFrame.From(new MinifyDto { Name = "Alice", Score = 200, Price = 9.99, IsActive = true, Id = 1 });
        var minCurrAll = MinifyDtoKeyFrame.From(new MinifyDto { Name = "Bob", Score = 200, Price = 19.99, IsActive = false, Id = 2 });
        var minCurrNone = MinifyDtoKeyFrame.From(new MinifyDto { Name = "Alice", Score = 100, Price = 9.99, IsActive = true, Id = 1 });
        var minDeltaOne  = MinifyDtoDeltaFrame.Create(minPrev, minCurrOne);
        var minDeltaAll  = MinifyDtoDeltaFrame.Create(minPrev, minCurrAll);
        var minDeltaNone = MinifyDtoDeltaFrame.Create(minPrev, minCurrNone);

        // Both (nullable mode, auto short names — existing PayloadDto)
        var bothPrev = PayloadDtoKeyFrame.From(V1);
        var bothCurrOne = PayloadDtoKeyFrame.From(V2OneChanged);
        var bothCurrAll = PayloadDtoKeyFrame.From(V2AllChanged);
        var bothCurrNone = PayloadDtoKeyFrame.From(V2NoneChanged);
        var bothDeltaOne  = PayloadDtoDeltaFrame.Create(bothPrev, bothCurrOne);
        var bothDeltaAll  = PayloadDtoDeltaFrame.Create(bothPrev, bothCurrAll);
        var bothDeltaNone = PayloadDtoDeltaFrame.Create(bothPrev, bothCurrNone);

        // --- Measure sizes ---
        // All modes use PropertyDeltaWrapper<T>? → WhenWritingNull to skip unchanged

        int Sz(object obj, JsonSerializerOptions opts) => JsonSerializer.SerializeToUtf8Bytes(obj, opts).Length;

        Console.WriteLine();
        Console.WriteLine("=== Mode Comparison: JSON Payload Sizes (bytes) ===");
        Console.WriteLine();
        Console.WriteLine(string.Format("  {0,-28} {1,8} {2,12} {3,12} {4,10}",
            "Scenario", "Plain", "Propagate", "MinifyJson", "Both"));
        Console.WriteLine("  " + new string('-', 74));

        // KeyFrame
        Console.WriteLine(string.Format("  {0,-28} {1,8} {2,12} {3,12} {4,10}",
            "KeyFrame (full state)",
            Sz(plainPrev, jsonNull),
            Sz(propPrev, jsonNull),
            Sz(minPrev, jsonNull),
            Sz(bothPrev, jsonNull)));

        // Delta all changed
        Console.WriteLine(string.Format("  {0,-28} {1,8} {2,12} {3,12} {4,10}",
            "Delta (all 5 changed)",
            Sz(plainDeltaAll, jsonNull),
            Sz(propDeltaAll, jsonNull),
            Sz(minDeltaAll, jsonNull),
            Sz(bothDeltaAll, jsonNull)));

        // Delta 1 changed
        Console.WriteLine(string.Format("  {0,-28} {1,8} {2,12} {3,12} {4,10}",
            "Delta (1 of 5 changed)",
            Sz(plainDeltaOne, jsonNull),
            Sz(propDeltaOne, jsonNull),
            Sz(minDeltaOne, jsonNull),
            Sz(bothDeltaOne, jsonNull)));

        // Delta none changed
        Console.WriteLine(string.Format("  {0,-28} {1,8} {2,12} {3,12} {4,10}",
            "Delta (none changed)",
            Sz(plainDeltaNone, jsonNull),
            Sz(propDeltaNone, jsonNull),
            Sz(minDeltaNone, jsonNull),
            Sz(bothDeltaNone, jsonNull)));

        Console.WriteLine();
        Console.WriteLine("  Modes:");
        Console.WriteLine("    Plain      = [StreamFrame]");
        Console.WriteLine("    Propagate  = [StreamFrame(PropagateAttributes = true)]  + user [JsonPropertyName]");
        Console.WriteLine("    MinifyJson = [StreamFrame(MinifyJson = true)]");
        Console.WriteLine("    Both       = [StreamFrame(PropagateAttributes = true, MinifyJson = true)]");
        Console.WriteLine();
        Console.WriteLine("  Notes:");
        Console.WriteLine("    - All modes use PropertyDeltaWrapper<T>? (null = unchanged, omitted with WhenWritingNull)");
        Console.WriteLine("    - All modes benefit from [JsonIgnore] on HasChanges");
        Console.WriteLine();

        // --- Sample JSON snippets ---
        Console.WriteLine("  === Sample: Delta (1 of 5 changed) ===");
        Console.WriteLine();
        var compactNull = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        Console.WriteLine("  Plain:      " + JsonSerializer.Serialize(plainDeltaOne, compactNull));
        Console.WriteLine("  Propagate:  " + JsonSerializer.Serialize(propDeltaOne, compactNull));
        Console.WriteLine("  MinifyJson: " + JsonSerializer.Serialize(minDeltaOne, compactNull));
        Console.WriteLine("  Both:       " + JsonSerializer.Serialize(bothDeltaOne, compactNull));
        Console.WriteLine();
    }
}
