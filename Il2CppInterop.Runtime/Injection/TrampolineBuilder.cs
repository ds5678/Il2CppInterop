using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Structs;

namespace Il2CppInterop.Runtime.Injection;

public static class TrampolineBuilder
{
    private static readonly AssemblyBuilder _fixedStructAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FixedSizeStructAssembly"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder _fixedStructModuleBuilder = _fixedStructAssembly.DefineDynamicModule("FixedSizeStructAssembly");
    private static readonly Dictionary<int, Type> _fixedStructCache = new();

    private static readonly AssemblyBuilder _delegateAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Il2CppTrampolineDelegates"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder _delegateModule = _delegateAssembly.DefineDynamicModule("Il2CppTrampolineDelegates");
    private static readonly ConcurrentDictionary<TrampolineSignatureHash, Type> ourDelegateTypes = new();

    private static Type GetFixedSizeStructType(int size)
    {
        if (_fixedStructCache.TryGetValue(size, out var result))
        {
            return result;
        }

        var tb = _fixedStructModuleBuilder.DefineType($"IL2CPPDetour_FixedSizeStruct_{size}b", TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed, typeof(ValueType));
        tb.DefineField("_element0", typeof(byte), FieldAttributes.Private);

        // Apply InlineArray attribute
        var data = new byte[8];
        data[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(2), size);
        tb.SetCustomAttribute(typeof(InlineArrayAttribute).GetConstructors()[0], data);

        var type = tb.CreateType();
        return _fixedStructCache[size] = type;
    }

    public static Type GetNativeType(Type managedType)
    {
        if (managedType == typeof(void))
        {
            return managedType;
        }
        else if (!managedType.IsValueType)
        {
            if (managedType.IsByRef)
            {
                throw new NotSupportedException("ByRef types are not supported in NativeType conversion.");
            }
            else if (managedType.IsArray || managedType.IsSZArray)
            {
                throw new NotSupportedException("Array types are not supported in NativeType conversion.");
            }

            // General reference type
            return typeof(IntPtr);
        }
        else if (managedType == typeof(Il2CppSystem.Boolean))
        {
            // bool is byte in Il2Cpp, but int in CLR => force size to be correct
            return typeof(byte);
        }
        else if (typeof(IByReference).IsAssignableFrom(managedType))
        {
            // ByReference types have no class, so we need this marker interface to identify them.
            return typeof(IntPtr);
        }
        else if (typeof(IPointer).IsAssignableFrom(managedType))
        {
            return typeof(IntPtr);
        }
        else
        {
            // Struct that's passed on the stack => handle as general struct

            var nativeClassPtr = Il2CppType.GetClassPointer(managedType);
            if (nativeClassPtr == IntPtr.Zero)
            {
                throw new NotSupportedException($"Type {managedType.FullName} is not an Il2Cpp type.");
            }

            var fixedSize = IL2CPP.il2cpp_class_value_size(nativeClassPtr, out _);
            return GetFixedSizeStructType(fixedSize);
        }
    }

    public static Type GetOrCreateDelegateType(MethodInfo monoMethod)
    {
        return ourDelegateTypes.GetOrAdd(new TrampolineSignatureHash(monoMethod), CreateDelegateType, monoMethod);
    }

    private static Type CreateDelegateType(TrampolineSignatureHash signatureHash, MethodInfo monoMethod)
    {
        var typeName = $"Il2CppToManagedDelegate_{signatureHash}";

        var newType = _delegateModule.DefineType(typeName, TypeAttributes.Sealed | TypeAttributes.Public,
            typeof(MulticastDelegate));

        newType.DefineConstructor(
            MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
            MethodAttributes.Public, CallingConventions.HasThis, [typeof(object), typeof(IntPtr)])
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        var parameterTypes = GetNativeParameters(monoMethod);

        newType.DefineMethod("Invoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            CallingConventions.HasThis,
            GetNativeType(monoMethod.ReturnType),
            parameterTypes)
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        newType.DefineMethod("BeginInvoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot |
            MethodAttributes.Public,
            CallingConventions.HasThis, typeof(IAsyncResult),
            parameterTypes.Concat([typeof(AsyncCallback), typeof(object)]).ToArray())
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        newType.DefineMethod("EndInvoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            CallingConventions.HasThis,
            GetNativeType(monoMethod.ReturnType),
            [typeof(IAsyncResult)])
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        return newType.CreateType();
    }

    private static Type[] GetNativeParameters(MethodInfo monoMethod) =>
    [
        ..(ReadOnlySpan<Type>)(monoMethod.IsStatic ? [] : [typeof(IntPtr)]),
        ..monoMethod.GetParameters().Select(it => GetNativeType(it.ParameterType)),
        typeof(Il2CppMethodInfo*),
    ];
}
