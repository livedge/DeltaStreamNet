using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaStreamNet;

public class FrameJsonConverterFactory : JsonConverterFactory
{
    private static readonly ConcurrentDictionary<Type, Type> PatchTypes = new();

    public static void Register<T>(Type patchType)
    {
        PatchTypes[typeof(T)] = patchType;
    }

    public static Type GetPatchType<T>()
    {
        if (!PatchTypes.TryGetValue(typeof(T), out var patchType))
            throw new InvalidOperationException(
                $"No patch type registered for {typeof(T).FullName}. " +
                $"Ensure the generated DeltaStreamContext has been accessed before deserializing.");
        return patchType;
    }

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(Frame<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var dtoType = typeToConvert.GetGenericArguments()[0];

        if (!PatchTypes.TryGetValue(dtoType, out var patchType))
            throw new InvalidOperationException(
                $"No patch type registered for {dtoType.FullName}. " +
                $"Ensure the generated DeltaStreamContext has been accessed before deserializing.");

        var converterType = typeof(FrameJsonConverter<>).MakeGenericType(dtoType);
        return (JsonConverter)Activator.CreateInstance(converterType, patchType)!;
    }
}
