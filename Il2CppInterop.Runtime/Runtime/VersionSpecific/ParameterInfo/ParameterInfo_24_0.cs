using System;
using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo
{
    [ApplicableToUnityVersionsSince("2018.3.0")]
    public unsafe class NativeParameterInfoStructHandler_24_0 : INativeParameterInfoStructHandler
    {
        public int Size() => sizeof(Il2CppParameterInfo_24_0);
        public INativeParameterInfoStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppParameterInfo_24_0* _ = (Il2CppParameterInfo_24_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeParameterInfoStruct Wrap(Il2CppParameterInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppParameterInfo_24_0
        {
            public byte* name;
            public int position;
            public uint token;
            public Il2CppTypeStruct* parameter_type;
        }
        internal class NativeStructWrapper : INativeParameterInfoStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppParameterInfo_24_0* _ => (Il2CppParameterInfo_24_0*)Pointer;
            public Il2CppParameterInfo* ParameterInfoPointer => (Il2CppParameterInfo*)Pointer;
            public bool HasNamePosToken => true;
            public ref IntPtr Name => ref *(IntPtr*)&_->name;
            public ref int Position => ref _->position;
            public ref uint Token => ref _->token;
            public ref Il2CppTypeStruct* ParameterType => ref _->parameter_type;
        }
    }
}
