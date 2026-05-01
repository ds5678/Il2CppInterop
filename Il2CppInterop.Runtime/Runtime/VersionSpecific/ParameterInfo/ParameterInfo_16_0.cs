using System;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo
{
    [ApplicableToUnityVersionsSince("5.3.0")]
    internal class NativeParameterInfoStructHandler_16_0 : INativeParameterInfoStructHandler
    {
        public unsafe int Size()
        {
            return sizeof(Il2CppParameterInfo_16_0);
        }

        public unsafe INativeParameterInfoStruct? Wrap(Il2CppParameterInfo* paramInfoPointer)
        {
            if ((IntPtr)paramInfoPointer == IntPtr.Zero) return null;
            return new NativeParameterInfoStructWrapper((IntPtr)paramInfoPointer);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct Il2CppParameterInfo_16_0
        {
            public IntPtr name; // const char*
            public int position;
            public uint token;
            public int customAttributeIndex;
            public Il2CppTypeStruct* parameter_type; // const
        }

        internal unsafe class NativeParameterInfoStructWrapper : INativeParameterInfoStruct
        {
            public NativeParameterInfoStructWrapper(IntPtr pointer)
            {
                Pointer = pointer;
            }

            private Il2CppParameterInfo_16_0* NativeParameter => (Il2CppParameterInfo_16_0*)Pointer;

            public IntPtr Pointer { get; }

            public Il2CppParameterInfo* ParameterInfoPointer => (Il2CppParameterInfo*)Pointer;

            public bool HasNamePosToken => true;

            public ref IntPtr Name => ref NativeParameter->name;

            public ref int Position => ref NativeParameter->position;

            public ref uint Token => ref NativeParameter->token;

            public ref Il2CppTypeStruct* ParameterType => ref NativeParameter->parameter_type;
        }
    }
}
