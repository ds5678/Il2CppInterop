using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;

namespace Il2CppInterop.Runtime.Extensions;

internal static class INativeMethodInfoStructExtensions
{
    public static unsafe ReadOnlySpan<byte> GetNameSpan(this INativeMethodInfoStruct methodInfo)
    {
        var namePtr = methodInfo.Name;
        if (namePtr == IntPtr.Zero)
            return default;

        // Find null terminator
        var length = 0;
        while (Marshal.ReadByte(namePtr, length) > 0)
        {
            length++;
        }
        return new ReadOnlySpan<byte>((void*)namePtr, length);
    }
}
