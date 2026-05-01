using System.CodeDom.Compiler;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppImageGenerator : VersionSpecificGenerator
{
    public Il2CppImageGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeImageStructHandler";
    protected override string HandlerInterface => "INativeImageStructHandler";
    protected override string NativeInterface => "INativeImageStruct";
    protected override string NativeStub => "Il2CppImage";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ImagePointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        },
        new CodeGenProperty("bool", ElementProtection.Public, "HasNameNoExt")
        {
            ImmediateGet = GetNativeField("nameNoExt") is not null ? "true" : "false"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("Il2CppAssembly*", "Assembly", ["assembly"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("byte", "Dynamic", ["dynamic"], makeDummyIfNotExist: true),
        new ByRefWrapper("IntPtr", "Name", ["name"]),
        new ByRefWrapper("IntPtr", "NameNoExt", ["nameNoExt"], addNotSupportedIfNotExist: true)
    ];

    protected override Action<IndentedTextWriter>? CreateNewExtraBody => writer =>
    {
        if (GetNativeField("metadataHandle") is not null)
        {
            writer.WriteLine("Il2CppImageGlobalMetadata* metadata = (Il2CppImageGlobalMetadata*)Marshal.AllocHGlobal(sizeof(Il2CppImageGlobalMetadata));");
            writer.WriteLine("metadata->image = (Il2CppImage*)_;");
            writer.WriteLine("*(Il2CppImageGlobalMetadata**)&_->metadataHandle = metadata;");
        }
    };
}
