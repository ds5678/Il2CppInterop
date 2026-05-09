using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Structs.VersionSpecific.EventInfo
{
    [ApplicableToUnityVersionsSince("5.2.2")]
    public unsafe class NativeEventInfoStructHandler_16_0 : INativeEventInfoStructHandler
    {
        public INativeEventInfoStruct CreateNewStruct()
        {
            nint ptr = Marshal.AllocHGlobal(Size);
            Il2CppEventInfo_16_0* _ = (Il2CppEventInfo_16_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeEventInfoStruct Wrap(Il2CppEventInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((nint)ptr);
        }
        public int Size => sizeof(Il2CppEventInfo_16_0);
        internal unsafe struct Il2CppEventInfo_16_0
        {
            public byte* name;
            public Il2CppTypeStruct* eventType;
            public Il2CppClass* parent;
            public Il2CppMethodInfo* add;
            public Il2CppMethodInfo* remove;
            public Il2CppMethodInfo* raise;
            public int customAttributeIndex;
        }
        internal class NativeStructWrapper : INativeEventInfoStruct
        {
            public NativeStructWrapper(nint ptr) => Pointer = ptr;
            public nint Pointer { get; }
            private Il2CppEventInfo_16_0* _ => (Il2CppEventInfo_16_0*)Pointer;
            public Il2CppEventInfo* EventInfoPointer => (Il2CppEventInfo*)Pointer;
            public ref nint Name => ref *(nint*)&_->name;
            public ref Il2CppTypeStruct* EventType => ref _->eventType;
            public ref Il2CppClass* Parent => ref _->parent;
            public ref Il2CppMethodInfo* Add => ref _->add;
            public ref Il2CppMethodInfo* Remove => ref _->remove;
            public ref Il2CppMethodInfo* Raise => ref _->raise;
        }
    }
}
