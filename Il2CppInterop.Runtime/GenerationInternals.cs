using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Exceptions;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

/// <summary>
/// Do not reference this class. Everything in it is an implementation detail of the generator. Breaking changes may occur at any time without warning.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GenerationInternals
{
    public static unsafe string? Il2CppStringToManaged(Il2CppSystem.String? il2CppString)
    {
        if (il2CppString == null)
            return null;

        var il2CppStringPtr = il2CppString.Pointer;

        var length = IL2CPP.il2cpp_string_length(il2CppStringPtr);
        var chars = IL2CPP.il2cpp_string_chars(il2CppStringPtr);

        return new string(chars, 0, length);
    }

    public static unsafe Il2CppSystem.String? ManagedStringToIl2Cpp(string? str)
    {
        if (str == null)
            return null;

        fixed (char* chars = str)
        {
            return new Il2CppSystem.String((ObjectPointer)IL2CPP.il2cpp_string_new_utf16(chars, str.Length));
        }
    }

    public static Il2CppSystem.Type ManagedTypeToIl2CppType(Type type)
    {
        var classPointer = Il2CppClassPointerStore.GetNativeClassPointer(type);
        if (classPointer == IntPtr.Zero)
        {
            throw new ArgumentException($"{type} does not have a corresponding IL2CPP class pointer");
        }

        var il2CppType = IL2CPP.il2cpp_class_get_type(classPointer);
        if (il2CppType == IntPtr.Zero)
        {
            throw new ArgumentException($"{type} does not have a corresponding IL2CPP type pointer");
        }

        return Il2CppSystem.Type.internal_from_handle(il2CppType);
    }

    public static nint Il2CppGCHandleGetTargetOrThrow(nint gchandle)
    {
        var obj = IL2CPP.il2cpp_gchandle_get_target(gchandle);
        if (obj == nint.Zero)
            throw new ObjectCollectedException("Object was garbage collected in IL2CPP domain");
        return obj;
    }

    public static bool Il2CppGCHandleGetTargetWasCollected(nint gchandle)
    {
        var obj = IL2CPP.il2cpp_gchandle_get_target(gchandle);
        return obj == nint.Zero;
    }

    public static nint GetIl2CppMethodByToken(nint clazz, int token)
    {
        if (clazz == nint.Zero)
            return GetMethodInfoForMissingMethod(token.ToString());

        var iter = nint.Zero;
        nint method;
        while ((method = IL2CPP.il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
            if (IL2CPP.il2cpp_method_get_token(method) == token)
                return method;

        var className = IL2CPP.il2cpp_class_get_name(clazz);
        Logger.Instance.LogTrace("Unable to find method {ClassName}::{Token}", className, token);

        return GetMethodInfoForMissingMethod($"{className}::{token}");
    }

    public static nint GetIl2CppMethod(nint clazz, bool isGeneric, string methodName, string returnTypeName, params string[] argTypes)
    {
        if (clazz == nint.Zero)
            return GetMethodInfoForMissingMethod($"{methodName}({string.Join(", ", argTypes)})");

        returnTypeName = Regex.Replace(returnTypeName, "\\`\\d+", "").Replace('/', '.').Replace('+', '.');
        for (var index = 0; index < argTypes.Length; index++)
        {
            var argType = argTypes[index];
            argTypes[index] = Regex.Replace(argType, "\\`\\d+", "").Replace('/', '.').Replace('+', '.');
        }

        var methodsSeen = 0;
        var lastMethod = nint.Zero;
        var iter = nint.Zero;
        nint method;
        while ((method = IL2CPP.il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
        {
            if (IL2CPP.il2cpp_method_get_name(method) != methodName)
                continue;

            if (IL2CPP.il2cpp_method_get_param_count(method) != argTypes.Length)
                continue;

            if (IL2CPP.il2cpp_method_is_generic(method) != isGeneric)
                continue;

            var returnType = IL2CPP.il2cpp_method_get_return_type(method);
            var returnTypeNameActual = IL2CPP.il2cpp_type_get_name(returnType);
            if (returnTypeNameActual != returnTypeName)
                continue;

            methodsSeen++;
            lastMethod = method;

            var badType = false;
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = IL2CPP.il2cpp_method_get_param(method, (uint)i);
                var typeName = IL2CPP.il2cpp_type_get_name(paramType);
                if (typeName != argTypes[i])
                {
                    badType = true;
                    break;
                }
            }

            if (badType) continue;

            return method;
        }

        var className = IL2CPP.il2cpp_class_get_name(clazz);

        if (methodsSeen == 1)
        {
            Logger.Instance.LogTrace(
                "Method {ClassName}::{MethodName} was stubbed with a random matching method of the same name", className, methodName);
            Logger.Instance.LogTrace(
                "Stubby return type/target: {LastMethod} / {ReturnTypeName}", IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_return_type(lastMethod)), returnTypeName);
            Logger.Instance.LogTrace("Stubby parameter types/targets follow:");
            for (var i = 0; i < argTypes.Length; i++)
            {
                var paramType = IL2CPP.il2cpp_method_get_param(lastMethod, (uint)i);
                var typeName = IL2CPP.il2cpp_type_get_name(paramType);
                Logger.Instance.LogTrace("    {TypeName} / {ArgType}", typeName, argTypes[i]);
            }

            return lastMethod;
        }

        Logger.Instance.LogTrace("Unable to find method {ClassName}::{MethodName}; signature follows", className, methodName);
        Logger.Instance.LogTrace("    return {ReturnTypeName}", returnTypeName);
        foreach (var argType in argTypes)
            Logger.Instance.LogTrace("    {ArgType}", argType);
        Logger.Instance.LogTrace("Available methods of this name follow:");
        iter = nint.Zero;
        while ((method = IL2CPP.il2cpp_class_get_methods(clazz, ref iter)) != nint.Zero)
        {
            if (IL2CPP.il2cpp_method_get_name(method) != methodName)
                continue;

            var nParams = IL2CPP.il2cpp_method_get_param_count(method);
            Logger.Instance.LogTrace("Method starts");
            Logger.Instance.LogTrace("     return {MethodTypeName}", IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_return_type(method)));
            for (var i = 0; i < nParams; i++)
            {
                var paramType = IL2CPP.il2cpp_method_get_param(method, (uint)i);
                var typeName = IL2CPP.il2cpp_type_get_name(paramType);
                Logger.Instance.LogTrace("    {TypeName}", typeName);
            }

            return method;
        }

        return GetMethodInfoForMissingMethod($"{className}::{methodName}({string.Join(", ", argTypes)})");
    }

    private static IntPtr GetMethodInfoForMissingMethod(string methodName)
    {
        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.Name = Marshal.StringToCoTaskMemUTF8(methodName);
        methodInfo.Slot = ushort.MaxValue;
        return methodInfo.Pointer;
    }
}
