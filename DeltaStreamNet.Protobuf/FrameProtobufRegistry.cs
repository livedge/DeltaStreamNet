using System.Collections.Concurrent;
using System.Reflection;
using ProtoBuf.Meta;

namespace DeltaStreamNet;

public static class FrameProtobufRegistry
{
    private static readonly ConcurrentDictionary<Type, bool> Registered = new();
    private static readonly ConcurrentDictionary<Type, bool> RegisteredWrappers = new();

    public static void Register<T>(Type patchType)
    {
        Register<T>(patchType, RuntimeTypeModel.Default);
    }

    public static void Register<T>(Type patchType, RuntimeTypeModel model)
    {
        if (!Registered.TryAdd(typeof(Frame<T>), true))
            return;

        // 1. Configure Frame<T> hierarchy
        var frameMetaType = model.Add(typeof(Frame<T>), false);
        frameMetaType.Add(1, nameof(Frame<T>.Type));
        frameMetaType.Add(2, nameof(Frame<T>.EncoderUuid));
        frameMetaType.Add(3, nameof(Frame<T>.Version));
        frameMetaType.Add(4, nameof(Frame<T>.Timestamp));
        frameMetaType.AddSubType(100, typeof(KeyFrame<T>));
        frameMetaType.AddSubType(101, typeof(DeltaFrame<T>));

        // 2. KeyFrame<T>
        model.Add(typeof(KeyFrame<T>), false).Add(1, nameof(KeyFrame<T>.Value));

        // 3. DeltaFrame<T> — Patch is IDeltaPatch<T>
        model.Add(typeof(DeltaFrame<T>), false).Add(1, nameof(DeltaFrame<T>.Patch));

        // 4. IDeltaPatch<T> → concrete patch type
        model.Add(typeof(IDeltaPatch<T>), false).AddSubType(1, patchType);

        // 5. Concrete patch type — attributes are emitted by the generator
        model.Add(patchType, true);

        // 6. Discover and configure PropertyDeltaWrapper<X> for each property in the patch type
        ConfigurePropertyWrappers(patchType, model);
    }

    private static Type? _propertyDeltaWrapperOpenType;

    private static void ConfigurePropertyWrappers(Type patchType, RuntimeTypeModel model)
    {
        // PropertyDeltaWrapper<T> is source-generated, so resolve it by name at runtime
        _propertyDeltaWrapperOpenType ??= patchType.Assembly.GetType("DeltaStreamNet.PropertyDeltaWrapper`1");

        if (_propertyDeltaWrapperOpenType == null)
            return;

        foreach (var prop in patchType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propType = prop.PropertyType;

            // Check for Nullable<PropertyDeltaWrapper<X>>
            if (!propType.IsGenericType || propType.GetGenericTypeDefinition() != typeof(Nullable<>))
                continue;

            var innerType = propType.GetGenericArguments()[0];
            if (!innerType.IsGenericType || innerType.GetGenericTypeDefinition() != _propertyDeltaWrapperOpenType)
                continue;

            if (!RegisteredWrappers.TryAdd(innerType, true))
                continue;

            var wrapperMeta = model.Add(innerType, false);
            wrapperMeta.Add(1, "Value");
        }
    }
}
