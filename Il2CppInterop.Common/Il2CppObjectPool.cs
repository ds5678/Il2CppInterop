using System.Collections.Concurrent;

namespace Il2CppInterop.Common;

public static class Il2CppObjectPool
{
    private static readonly ConcurrentDictionary<nint, WeakReference<object>> s_cache = new();

    private static readonly ConcurrentDictionary<nint, Func<ObjectPointer, object>> s_initializers = new();

    public static void Remove(nint ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    public static object? Get(nint ptr)
    {
        if (ptr == nint.Zero)
            return null;

        if (s_cache.TryGetValue(ptr, out var reference) && reference.TryGetTarget(out var cachedObject))
        {
            return cachedObject;
        }

        var ownClass = IL2CPP.il2cpp_object_get_class(ptr);
        if (!s_initializers.TryGetValue(ownClass, out var initializer))
        {
            var className = IL2CPP.il2cpp_class_get_name(ownClass);
            throw new InvalidOperationException($"No initializer found for class {className}");
        }

        var newObj = initializer((ObjectPointer)ptr);
        if (!newObj.GetType().IsValueType)
        {
            s_cache[ptr] = new WeakReference<object>(newObj);
        }

        return newObj;
    }

    private static void RegisterInitializer(nint classPtr, Func<ObjectPointer, object> initializer)
    {
        ArgumentOutOfRangeException.ThrowIfZero(classPtr);
        if (!s_initializers.TryAdd(classPtr, initializer))
        {
            var className = IL2CPP.il2cpp_class_get_name(classPtr);
            throw new InvalidOperationException($"Initializer for class {className} is already registered");
        }
    }

    internal static void RegisterInitializer<T>() where T : IIl2CppType<T>
    {
        RegisterInitializer(Il2CppType.GetClassPointer<T>(), TypeInitializer<T>);
    }

    private static object TypeInitializer<T>(ObjectPointer obj) where T : IIl2CppType<T>
    {
        return T.UnboxNative(obj)!;
    }
}
