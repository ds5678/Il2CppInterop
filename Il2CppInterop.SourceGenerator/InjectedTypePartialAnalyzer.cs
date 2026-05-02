using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectedTypePartialAnalyzer : DiagnosticAnalyzer
{
    #region Diagnostic Descriptors

    private static readonly DiagnosticDescriptor MustBePartial = new(
        id: "IL2CPP0001",
        title: "Injected type must be partial",
        messageFormat: "Type '{0}' is marked with [InjectedType] but is not declared as partial. Add the 'partial' modifier so the source generator can augment it.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types decorated with [InjectedTypeAttribute] must be partial. " +
                     "The Il2CppTypeGenerator source generator needs to add members to the type, " +
                     "which requires the partial keyword.",
        helpLinkUri: "https://github.com/BepInEx/Il2CppInterop");

    private static readonly DiagnosticDescriptor MustInheritIl2CppObject = new(
        id: "IL2CPP0002",
        title: "Injected class must inherit from Il2CppSystem.Object",
        messageFormat: "Class '{0}' is marked with [InjectedType] but does not inherit from Il2CppSystem.Object",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CannotHaveStaticConstructor = new(
        id: "IL2CPP0003",
        title: "Injected type cannot have a static constructor",
        messageFormat: "Type '{0}' is marked with [InjectedType] but has a static constructor. Remove it, as Il2CppInternals handles static initialization.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CannotOverrideIl2CppFinalize = new(
        id: "IL2CPP0004",
        title: "Injected type should not manually override Il2CppFinalize",
        messageFormat: "Type '{0}' manually overrides Il2CppFinalize. Use [Il2CppFinalizer] on a method instead.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CannotHaveManagedFieldOnStructOrInterface = new(
        id: "IL2CPP0005",
        title: "Structs and interfaces cannot have [ManagedField] properties",
        messageFormat: "Type '{0}' is a {1} and cannot have properties marked with [ManagedField]",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CannotHaveIl2CppFieldOnStruct = new(
        id: "IL2CPP0006",
        title: "Structs cannot have instance properties marked with [Il2CppField]",
        messageFormat: "Type '{0}' is a struct and cannot have instance properties marked with [Il2CppField]",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ManagedFieldMustBeInstance = new(
        id: "IL2CPP0007",
        title: "Properties marked with [ManagedField] must be instance members",
        messageFormat: "Property '{0}' is marked with [ManagedField] but is static. [ManagedField] properties must be instance members.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor FieldPropertyMustBePartial = new(
        id: "IL2CPP0008",
        title: "Properties marked with [ManagedField] or [Il2CppField] must be partial",
        messageFormat: "Property '{0}' is marked with [{1}] but is not partial. Add the 'partial' modifier.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ShouldNotHaveUninjectedInstanceMembers = new(
        id: "IL2CPP0009",
        title: "Injected types should not have uninjected instance members",
        messageFormat: "Type '{0}' has instance member '{1}' which is not managed by Il2CppInterop. Use [ManagedField] or [Il2CppField] properties instead.",
        category: "Il2CppInterop",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        MustBePartial,
        MustInheritIl2CppObject,
        CannotHaveStaticConstructor,
        CannotOverrideIl2CppFinalize,
        CannotHaveManagedFieldOnStructOrInterface,
        CannotHaveIl2CppFieldOnStruct,
        ManagedFieldMustBeInstance,
        FieldPropertyMustBePartial,
        ShouldNotHaveUninjectedInstanceMembers,
    ];

    #endregion

    #region Registration

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeTypeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    #endregion

    #region Analysis

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var tds = (TypeDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(tds, context.CancellationToken);
        if (symbol is null)
            return;

        // Gate everything behind the attribute — no [InjectedType], no diagnostics
        if (!symbol.HasAttribute("InjectedTypeAttribute", ["Il2CppInterop", "Common", "Attributes"]))
            return;

        CheckMustBePartial(context, tds, symbol);
        CheckMustInheritIl2CppObject(context, tds, symbol);
        CheckNoStaticConstructor(context, tds, symbol);
        CheckNoIl2CppFinalizeOverride(context, tds, symbol);
        CheckNoManagedFieldOnStructOrInterface(context, tds, symbol);
        CheckNoIl2CppFieldOnStruct(context, tds, symbol);
        CheckManagedFieldMustBeInstance(context, tds);
        CheckFieldPropertiesMustBePartial(context, tds);
        CheckNoUninjectedInstanceMembers(context, tds, symbol);
    }

    #endregion

    #region Syntax Helpers

    private static bool HasAttribute(PropertyDeclarationSyntax prop, params string[] names) =>
        prop.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => names.Contains(a.Name.ToString()));

    #endregion

    #region Checks

    // IL2CPP0001
    private static void CheckMustBePartial(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds.Modifiers.Any(SyntaxKind.PartialKeyword))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            MustBePartial,
            tds.Identifier.GetLocation(),
            symbol.Name));
    }

    // IL2CPP0002
    private static void CheckMustInheritIl2CppObject(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not ClassDeclarationSyntax || symbol.TypeKind != TypeKind.Class)
            return;

        var baseType = symbol.BaseType;

        var isSystemObject = baseType is null || baseType.IsType("Object", ["System"]);

        var inheritsIl2CppObject = false;
        var current = baseType;
        while (current is not null)
        {
            if (current.IsType("Object", ["Il2CppSystem"]))
            {
                inheritsIl2CppObject = true;
                break;
            }
            current = current.BaseType;
        }

        if (isSystemObject || !inheritsIl2CppObject)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MustInheritIl2CppObject,
                tds.Identifier.GetLocation(),
                symbol.Name));
        }
    }

    // IL2CPP0003
    private static void CheckNoStaticConstructor(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        var staticCtor = tds.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.Modifiers.Any(SyntaxKind.StaticKeyword));

        if (staticCtor is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            CannotHaveStaticConstructor,
            staticCtor.Identifier.GetLocation(),
            symbol.Name));
    }

    // IL2CPP0004
    private static void CheckNoIl2CppFinalizeOverride(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        var finalizeOverride = tds.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
                m.Identifier.Text == "Il2CppFinalize" &&
                m.Modifiers.Any(SyntaxKind.OverrideKeyword));

        if (finalizeOverride is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            CannotOverrideIl2CppFinalize,
            finalizeOverride.Identifier.GetLocation(),
            symbol.Name));
    }

    // IL2CPP0005
    private static void CheckNoManagedFieldOnStructOrInterface(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not (StructDeclarationSyntax or InterfaceDeclarationSyntax))
            return;

        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => HasAttribute(p, "ManagedField", "ManagedFieldAttribute"));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CannotHaveManagedFieldOnStructOrInterface,
                prop.Identifier.GetLocation(),
                symbol.Name,
                symbol.TypeKind == TypeKind.Struct ? "struct" : "interface"));
        }
    }

    // IL2CPP0006
    private static void CheckNoIl2CppFieldOnStruct(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        if (tds is not StructDeclarationSyntax)
            return;

        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p =>
                !p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                HasAttribute(p, "Il2CppField", "Il2CppFieldAttribute"));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CannotHaveIl2CppFieldOnStruct,
                prop.Identifier.GetLocation(),
                symbol.Name));
        }
    }

    // IL2CPP0007
    private static void CheckManagedFieldMustBeInstance(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds)
    {
        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p =>
                p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                HasAttribute(p, "ManagedField", "ManagedFieldAttribute"));

        foreach (var prop in offending)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ManagedFieldMustBeInstance,
                prop.Identifier.GetLocation(),
                prop.Identifier.Text));
        }
    }

    // IL2CPP0008
    private static void CheckFieldPropertiesMustBePartial(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds)
    {
        var offending = tds.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => !p.Modifiers.Any(SyntaxKind.PartialKeyword));

        foreach (var prop in offending)
        {
            var matchingAttr = prop.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString() is
                    "ManagedField" or "ManagedFieldAttribute" or
                    "Il2CppField" or "Il2CppFieldAttribute");

            if (matchingAttr is null)
                continue;

            var attrName = matchingAttr.Name.ToString().Replace("Attribute", "");

            context.ReportDiagnostic(Diagnostic.Create(
                FieldPropertyMustBePartial,
                prop.Identifier.GetLocation(),
                prop.Identifier.Text,
                attrName));
        }
    }

    // IL2CPP0009
    private static void CheckNoUninjectedInstanceMembers(
        SyntaxNodeAnalysisContext context, TypeDeclarationSyntax tds, INamedTypeSymbol symbol)
    {
        // Plain instance fields
        foreach (var field in tds.Members.OfType<FieldDeclarationSyntax>()
                     .Where(f => !f.Modifiers.Any(SyntaxKind.StaticKeyword)))
        {
            foreach (var variable in field.Declaration.Variables)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ShouldNotHaveUninjectedInstanceMembers,
                    variable.Identifier.GetLocation(),
                    symbol.Name,
                    variable.Identifier.Text));
            }
        }

        // Instance properties without [ManagedField] or [Il2CppField]
        foreach (var prop in tds.Members.OfType<PropertyDeclarationSyntax>()
                     .Where(p =>
                         !p.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                         !HasAttribute(p, "ManagedField", "ManagedFieldAttribute") &&
                         !HasAttribute(p, "Il2CppField", "Il2CppFieldAttribute")))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ShouldNotHaveUninjectedInstanceMembers,
                prop.Identifier.GetLocation(),
                symbol.Name,
                prop.Identifier.Text));
        }
    }

    #endregion
}
