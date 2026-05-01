using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppPropertyInfoGenerator : VersionSpecificGenerator
{
    public Il2CppPropertyInfoGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string CppClassName => "Il2CppPropertyInfo";
    protected override string HandlerName => "NativePropertyInfoStructHandler";
    protected override string HandlerInterface => "INativePropertyInfoStructHandler";
    protected override string NativeInterface => "INativePropertyInfoStruct";
    protected override string NativeStub => "Il2CppPropertyInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "PropertyInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("IntPtr", "Name", ["name"]),
        new ByRefWrapper("Il2CppClass*", "Parent", ["parent"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Get", ["get"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Set", ["set"]),
        new ByRefWrapper("uint", "Attrs", ["attrs"])
    ];
}
