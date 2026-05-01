using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppAssemblyNameGenerator : VersionSpecificGenerator
{
    public Il2CppAssemblyNameGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    protected override string HandlerName => "NativeAssemblyNameStructHandler";
    protected override string HandlerInterface => "INativeAssemblyNameStructHandler";
    protected override string NativeInterface => "INativeAssemblyNameStruct";
    protected override string NativeStub => "Il2CppAssemblyName";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "AssemblyNamePointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("IntPtr", "Name", ["name", "nameIndex"]),
        new ByRefWrapper("IntPtr", "Culture", ["culture", "cultureIndex"]),
        new ByRefWrapper("IntPtr", "PublicKey", ["public_key", "publicKeyIndex"]),
        new ByRefWrapper("int", "Major", ["major"]),
        new ByRefWrapper("int", "Minor", ["minor"]),
        new ByRefWrapper("int", "Build", ["build"]),
        new ByRefWrapper("int", "Revision", ["revision"]),
        new ByRefWrapper("ulong", "PublicKeyToken", ["public_key_token", "publicKeyToken"])
    ];
}
