using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppParameterInfoGenerator : VersionSpecificGenerator
{
    public Il2CppParameterInfoGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeParameterInfoStructHandler";
    protected override string HandlerInterface => "INativeParameterInfoStructHandler";
    protected override string NativeInterface => "INativeParameterInfoStruct";
    protected override string NativeStub => "Il2CppParameterInfo";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ParameterInfoPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" },
        new CodeGenProperty("bool", ElementProtection.Public, "HasNamePosToken")
        { ImmediateGet = GetNativeField("name") is not null ? "true" : "false" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }, addNotSupportedIfNotExist: true),
        new ByRefWrapper("int", "Position", new[] { "position" }, addNotSupportedIfNotExist: true),
        new ByRefWrapper("uint", "Token", new[] { "token" }, addNotSupportedIfNotExist: true),
        new ByRefWrapper("Il2CppTypeStruct*", "ParameterType", new[] { "parameter_type" }),
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
