using System;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo
{
    [ApplicableToUnityVersionsSince("2021.2.0")]
    internal class NativeParameterInfoStructHandler_27_0 : INativeParameterInfoStructHandler
    {
        public unsafe int Size()
        {
            return sizeof(Il2CppParameterInfo_27_0);
        }

        public unsafe INativeParameterInfoStruct? Wrap(Il2CppParameterInfo* paramInfoPointer)
        {
            if ((IntPtr)paramInfoPointer == IntPtr.Zero) return null;
            return new NativeParameterInfoStructWrapper((IntPtr)paramInfoPointer);
        }

        public bool HasNamePosToken => false;

        //Doesn't actually exist; just using this for type pointer storage in MethodInfo 27_3 +
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct Il2CppParameterInfo_27_0
        {
            public Il2CppTypeStruct* parameter_type;
        }

        internal unsafe class NativeParameterInfoStructWrapper : INativeParameterInfoStruct
        {
            public NativeParameterInfoStructWrapper(IntPtr pointer)
            {
                Pointer = pointer;
            }

            private Il2CppParameterInfo_27_0* NativeParameter => (Il2CppParameterInfo_27_0*)Pointer;

            public IntPtr Pointer { get; }

            public Il2CppParameterInfo* ParameterInfoPointer => (Il2CppParameterInfo*)Pointer;

            public bool HasNamePosToken => false;

            public ref IntPtr Name => throw new NotSupportedException("ParameterInfo does not exist in Unity 2021.2.0+");

            public ref int Position => throw new NotSupportedException("ParameterInfo does not exist in Unity 2021.2.0+");

            public ref uint Token => throw new NotSupportedException("ParameterInfo does not exist in Unity 2021.2.0+");

            public ref Il2CppTypeStruct* ParameterType => ref NativeParameter->parameter_type;
        }
    }
}
