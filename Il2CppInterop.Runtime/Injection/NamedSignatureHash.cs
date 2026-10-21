using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Reflection;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Structs;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;

namespace Il2CppInterop.Runtime.Injection;

/// <summary>
/// A hash of a method's name and signature: the name, whether the method is static and whether it is generic, the number of
/// parameters, and the full names of the return type and the parameter types. Each name ends with a character that no
/// name contains, so that the parts of two different signatures cannot add up to the same bytes.
/// </summary>
/// <remarks>
/// Methods of different classes can have the same name and signature, such as a method and a base class's method that it
/// hides with <c>new</c>; <see cref="TypeInjector"/> matches methods by their declaring class as well.
/// </remarks>
internal readonly record struct NamedSignatureHash
{
    private const byte NameEnd = 0;

    private readonly UInt128 _hash;

    public unsafe NamedSignatureHash(INativeMethodInfoStruct methodInfo)
    {
        XxHash128 hash = new();
        hash.Append(methodInfo.GetNameSpan());
        hash.Append(NameEnd);
        hash.Append((methodInfo.Flags & Il2CppMethodFlags.METHOD_ATTRIBUTE_STATIC) != 0);
        // TODO: Hash the generic arity instead, in both constructors.
        hash.Append(methodInfo.IsGeneric);
        hash.Append(methodInfo.ParametersCount);
        AppendTypeName(hash, GetFullName(methodInfo.ReturnType));
        for (var i = 0; i < methodInfo.ParametersCount; i++)
        {
            var parameter = UnityVersionHandler.Wrap(methodInfo.Parameters, i);
            AppendTypeName(hash, GetFullName(parameter.ParameterType));
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    [RequiresDynamicCode("")]
    public NamedSignatureHash(MethodInfo methodInfo)
    {
        XxHash128 hash = new();
        var name = methodInfo.GetCustomAttribute<Il2CppMethodAttribute>()?.Name ?? methodInfo.Name;
        hash.AppendUtf8(name);
        hash.Append(NameEnd);
        hash.Append(methodInfo.IsStatic);
        hash.Append(methodInfo.IsGenericMethodDefinition);
        var parameters = methodInfo.GetParameters();
        hash.Append((byte)parameters.Length);
        AppendTypeName(hash, GetFullName(methodInfo.ReturnType));
        foreach (var parameter in parameters)
        {
            AppendTypeName(hash, GetFullName(parameter.ParameterType));
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    // The full name of a generic parameter is null, so it adds no characters before the end of the name.
    private static void AppendTypeName(XxHash128 hash, Il2CppSystem.String? fullName)
    {
        hash.Append(fullName);
        hash.Append("\0".AsSpan());
    }

    [RequiresDynamicCode("")]
    private static unsafe Il2CppSystem.String GetFullName(Type type)
    {
        return GetFullName((Il2CppTypeStruct*)Il2CppTypePointerStore.GetNativeTypePointer(type));
    }

    private static unsafe Il2CppSystem.String GetFullName(Il2CppTypeStruct* type)
    {
        return Il2CppSystem.Type.FromTypePointer((nint)type).FullName;
    }

    public override string ToString() => _hash.ToString();
}
