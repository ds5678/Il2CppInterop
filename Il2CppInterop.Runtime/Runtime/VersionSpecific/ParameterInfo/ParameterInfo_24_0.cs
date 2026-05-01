using System;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo
{
    [ApplicableToUnityVersionsSince("2018.3.0")]
    internal unsafe class _InfoStructHandler_24_0 : INativeParameterInfoStructHandler
    {
        public int Size() => sizeof(Il2CppParameterInfo_24_0);
        public INativeParameterInfoStruct? Wrap(Il2CppParameterInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct Il2CppParameterInfo_24_0
        {
            public IntPtr name; // const char*
            public int position;
            public uint token;
            public Il2CppTypeStruct* parameter_type; // const
        }

        internal class NativeStructWrapper : INativeParameterInfoStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;

            public IntPtr Pointer { get; }

            private Il2CppParameterInfo_24_0* _ => (Il2CppParameterInfo_24_0*)Pointer;

            public Il2CppParameterInfo* ParameterInfoPointer => (Il2CppParameterInfo*)Pointer;

            public bool HasNamePosToken => true;

            public ref IntPtr Name => ref _->name;

            public ref int Position => ref _->position;

            public ref uint Token => ref _->token;

            public ref Il2CppTypeStruct* ParameterType => ref _->parameter_type;
        }
    }
}
