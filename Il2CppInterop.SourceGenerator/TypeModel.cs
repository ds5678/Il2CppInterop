using Microsoft.CodeAnalysis;

namespace Il2CppInterop.SourceGenerator;

internal sealed record TypeModel(
    string? Namespace,
    string TypeName,
    TypeKind TypeKind, // "class", "struct", or "interface"
    string? AssemblyName,
    EquatableArray<MemberModel> Members,
    EquatableArray<string> FinalizerMethodNames,
    bool NeedsObjectPointerConstructor,
    Accessibility? DeclaredAccessibility,
    bool IsAbstract);
