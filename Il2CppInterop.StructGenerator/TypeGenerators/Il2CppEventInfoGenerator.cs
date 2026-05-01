using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppEventInfoGenerator : VersionSpecificGenerator
{
    public Il2CppEventInfoGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeEventInfoStructHandler";
    protected override string HandlerInterface => "INativeEventInfoStructHandler";
    protected override string NativeInterface => "INativeEventInfoStruct";
    protected override string NativeStub => "Il2CppEventInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "EventInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("IntPtr", "Name", ["name"]),
        new ByRefWrapper("Il2CppTypeStruct*", "EventType", ["eventType"]),
        new ByRefWrapper("Il2CppClass*", "Parent", ["parent"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Add", ["add"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Remove", ["remove"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Raise", ["raise"])
    ];
}
