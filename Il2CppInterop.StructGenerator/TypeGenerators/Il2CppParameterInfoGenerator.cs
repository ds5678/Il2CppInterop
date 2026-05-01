using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppParameterInfoGenerator : VersionSpecificGenerator
{
    public Il2CppParameterInfoGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string CppClassName => "Il2CppParameterInfo";
    protected override string HandlerName => "NativeParameterInfoStructHandler";
    protected override string HandlerInterface => "INativeParameterInfoStructHandler";
    protected override string NativeInterface => "INativeParameterInfoStruct";
    protected override string NativeStub => "Il2CppParameterInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ParameterInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        },
        new CodeGenProperty("bool", ElementProtection.Public, "HasNamePosToken")
        {
            ImmediateGet = GetNativeField("name") is not null ? "true" : "false"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("IntPtr", "Name", ["name"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("int", "Position", ["position"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("uint", "Token", ["token"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("Il2CppTypeStruct*", "ParameterType", ["parameter_type"]),
    ];
}
