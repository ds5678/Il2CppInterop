using Il2CppInterop.Common;
using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public abstract class Type : Object, IIl2CppType<Type>
{
    static int IIl2CppType<Type>.Size => throw null;
    nint IIl2CppType.ObjectClass => throw null;
    static Type IIl2CppType<Type>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Type>.WriteToSpan(Type value, System.Span<byte> span) => throw null;

    public RuntimeTypeHandle _impl { get; set; }

    public abstract Type GetNestedType(String name, BindingFlags bindingAttr);

    public static Type internal_from_handle(IntPtr handle)
    {
        throw null;
    }

    public virtual RuntimeTypeHandle TypeHandle
    {
        get
        {
            throw null;
        }
    }

    public Boolean IsPrimitive
    {
        get
        {
            throw null;
        }
    }

    public Boolean IsByRef
    {
        get
        {
            throw null;
        }
    }

    public MethodInfo GetMethod(String name)
    {
        throw null;
    }

    public abstract String FullName { get; }

    public Type MakeByRefType() => throw null;

    public Type MakeGenericType(Type[] typeArguments) => throw null;

    public Type MakePointerType() => throw null;
}
