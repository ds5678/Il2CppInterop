using System;
using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo
{
    [ApplicableToUnityVersionsSince("2021.2.0")]
    public unsafe class NativeParameterInfoStructHandler_27_0 : INativeParameterInfoStructHandler
    {
        public int Size() => sizeof(Il2CppParameterInfo_27_0);
        public INativeParameterInfoStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppParameterInfo_27_0* _ = (Il2CppParameterInfo_27_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeParameterInfoStruct Wrap(Il2CppParameterInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppParameterInfo_27_0
        {
            public Il2CppTypeStruct* parameter_type;
        }
        internal class NativeStructWrapper : INativeParameterInfoStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppParameterInfo_27_0* _ => (Il2CppParameterInfo_27_0*)Pointer;
            public Il2CppParameterInfo* ParameterInfoPointer => (Il2CppParameterInfo*)Pointer;
            public bool HasNamePosToken => false;
            public ref IntPtr Name => throw new NotSupportedException();
            public ref int Position => throw new NotSupportedException();
            public ref uint Token => throw new NotSupportedException();
            public ref Il2CppTypeStruct* ParameterType => ref _->parameter_type;
        }
    }
}
