using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppTypeGenerator : VersionSpecificGenerator
{
    public Il2CppTypeGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string CppClassName => "Il2CppType";
    protected override string HandlerName => "NativeTypeStructHandler";
    protected override string HandlerInterface => "INativeTypeStructHandler";
    protected override string NativeInterface => "INativeTypeStruct";
    protected override string NativeStub => "Il2CppTypeStruct";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "TypePointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("IntPtr", "Data", ["data"]),
        new ByRefWrapper("ushort", "Attrs", ["attrs"]),
        new ByRefWrapper("Il2CppTypeEnum", "Type", ["type"])
    ];

    protected override IReadOnlyList<BitfieldAccessor>? BitfieldAccessors =>
    [
        new BitfieldAccessor("ByRef", "byref"),
        new BitfieldAccessor("Pinned", "pinned"),
        // maybe throw if not exist
        new BitfieldAccessor("ValueType", "valuetype", defaultGetter: "false")
    ];
}
