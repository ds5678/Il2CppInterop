using Il2CppInterop.Runtime.Runtime.VersionSpecific.Type;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.Class
{
    public interface INativeClassStructHandler : INativeStructHandler
    {
        INativeClassStruct CreateNewStruct(int vTableSlots);
        unsafe INativeClassStruct Wrap(Il2CppClass* pointer);
    }
    public interface INativeClassStruct : INativeStruct
    {
        nint VTable { get; }
        unsafe Il2CppClass* ClassPointer { get; }
        INativeTypeStruct ByValArg { get; }
        INativeTypeStruct ThisArg { get; }
        ref uint InstanceSize { get; }
        ref ushort VtableCount { get; }
        ref ushort InterfaceCount { get; }
        ref ushort InterfaceOffsetsCount { get; }
        ref byte TypeHierarchyDepth { get; }
        ref int NativeSize { get; }
        ref uint ActualSize { get; }
        ref ushort MethodCount { get; }
        ref ushort FieldCount { get; }
        ref Il2CppClassAttributes Flags { get; }
        ref nint Name { get; }
        ref nint Namespace { get; }
        unsafe ref Il2CppImage* Image { get; }
        unsafe ref Il2CppClass* Parent { get; }
        unsafe ref Il2CppClass* ElementClass { get; }
        unsafe ref Il2CppClass* CastClass { get; }
        unsafe ref Il2CppClass* DeclaringType { get; }
        unsafe ref Il2CppClass* Class { get; }
        unsafe ref Il2CppFieldInfo* Fields { get; }
        unsafe ref Il2CppMethodInfo** Methods { get; }
        unsafe ref Il2CppClass** ImplementedInterfaces { get; }
        unsafe ref Il2CppRuntimeInterfaceOffsetPair* InterfaceOffsets { get; }
        unsafe ref Il2CppClass** TypeHierarchy { get; }
        bool ValueType { get; set; }
        bool Initialized { get; set; }
        bool EnumType { get; set; }
        bool IsGeneric { get; set; }
        bool HasReferences { get; set; }
        bool SizeInited { get; set; }
        bool HasFinalize { get; set; }
        bool IsVtableInitialized { get; set; }
        bool InitializedAndNoError { get; set; }
    }
}
