using System.CodeDom.Compiler;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Il2CppInterop.SourceGenerator;

[Generator]
public sealed class Il2CppTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Il2CppInterop.Common.Attributes.InjectedTypeAttribute",
                predicate: static (node, _) =>
                {
                    if (node is not TypeDeclarationSyntax tds)
                        return false;
                    if (!tds.Modifiers.Any(SyntaxKind.PartialKeyword))
                        return false;
                    // Type must be non-generic
                    return tds switch
                    {
                        ClassDeclarationSyntax s => s.TypeParameterList is null,
                        InterfaceDeclarationSyntax s => s.TypeParameterList is null,
                        StructDeclarationSyntax s => s.TypeParameterList is null,
                        _ => false,
                    };
                },
                transform: static (ctx, ct) => GetTypeModel(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(types, static (ctx, model) => Emit(ctx, model!));
    }

    private static TypeModel? GetTypeModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type)
            return null;

        var typeKind = ctx.TargetNode switch
        {
            StructDeclarationSyntax    => TypeKind.Struct,
            InterfaceDeclarationSyntax => TypeKind.Interface,
            ClassDeclarationSyntax     => TypeKind.Class,
            _                          => TypeKind.Unknown,
        };

        if (typeKind == TypeKind.Unknown)
            return null;

        // Reference-type-only filter from the original class generator: skip plain `: object`.
        if (typeKind == TypeKind.Class && type.BaseType?.SpecialType == SpecialType.System_Object)
            return null;

        var injectedTypeAttr = ctx.Attributes[0];
        var assemblyName = injectedTypeAttr.NamedArguments
            .FirstOrDefault(a => a.Key == "Assembly")
            .Value.Value as string;

        var members = ImmutableArray.CreateBuilder<MemberModel>();
        var index = 0;

        if (typeKind == TypeKind.Struct)
        {
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                ct.ThrowIfCancellationRequested();

                var attrs = field.GetAttributes();
                if (!attrs.Any(a => a.AttributeClass.IsType("Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"])))
                    continue;

                members.Add(new MemberModel(
                    Name: field.Name,
                    Type: field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Kind: MemberKind.Il2Cpp,
                    Index: index++,
                    Accessibility: null,
                    IsStatic: field.IsStatic
                ));
            }
        }
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            ct.ThrowIfCancellationRequested();

            var attrs = property.GetAttributes();
            var il2cpp = attrs.Any(a => a.AttributeClass.IsType("Il2CppFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));
            var managed = attrs.Any(a => a.AttributeClass.IsType("ManagedFieldAttribute", ["Il2CppInterop", "Common", "Attributes"]));

            if (!il2cpp && !managed) continue;
            if (!property.IsPartialDefinition) continue;
            if (managed && property.IsStatic) continue;

            // DeclaredAccessibility defaults to Private when no modifier is written,
            // so we check the syntax directly to distinguish explicit "private" from omitted.
            var explicitAccess = property.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax(ct))
                .OfType<PropertyDeclarationSyntax>()
                .SelectMany(s => s.Modifiers)
                .Any(m => m.IsKind(SyntaxKind.PublicKeyword)
                          || m.IsKind(SyntaxKind.InternalKeyword)
                          || m.IsKind(SyntaxKind.PrivateKeyword)
                          || m.IsKind(SyntaxKind.ProtectedKeyword));

            members.Add(new MemberModel(
                Name: property.Name,
                Type: property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Kind: managed ? MemberKind.Managed : MemberKind.Il2Cpp,
                Index: index++,
                Accessibility: !explicitAccess ? null : property.DeclaredAccessibility,
                IsStatic: property.IsStatic));
        }

        var finalizerMethods = ImmutableArray<string>.Empty;
        var needsObjectPointerConstructor = false;

        if (typeKind == TypeKind.Class)
        {
            finalizerMethods = [
                ..type.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m =>
                        !m.IsStatic &&
                        m.Parameters.IsEmpty &&
                        m.ReturnsVoid &&
                        m.GetAttributes().Any(a =>
                            a.AttributeClass.IsType("Il2CppFinalizerAttribute", ["Il2CppInterop", "Common", "Attributes"])))
                    .Select(m => m.Name)
            ];

            needsObjectPointerConstructor = !type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.MethodKind == MethodKind.Constructor &&
                          m.Parameters is [{ Type: INamedTypeSymbol s }] &&
                          s.IsType("ObjectPointer", ["Il2CppInterop", "Common"]));
        }

        return new TypeModel(
            Namespace: type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(),
            TypeName: type.Name,
            TypeKind: typeKind,
            AssemblyName: assemblyName,
            Members: new EquatableArray<MemberModel>(members),
            FinalizerMethodNames: new EquatableArray<string>(finalizerMethods),
            NeedsObjectPointerConstructor: needsObjectPointerConstructor,
            DeclaredAccessibility: type.DeclaredAccessibility,
            IsAbstract: type.IsAbstract);
    }

    private static void Emit(SourceProductionContext ctx, TypeModel model)
    {
        using var sw = new StringWriter();
        using var writer = new IndentedTextWriter(sw, "    ");

        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine("#nullable enable");
        writer.WriteLine();

        if (model.Namespace is not null)
        {
            writer.WriteLine($"namespace {model.Namespace}");
            writer.WriteLine("{");
            writer.Indent++;
        }

        EmitPartialType(writer, model);
        writer.WriteLine();
        EmitInternalsClass(writer, model);

        if (model.Namespace is not null)
        {
            writer.Indent--;
            writer.WriteLine("}");
        }

        var hint = model.Namespace is null
            ? $"{model.TypeName}.g.cs"
            : $"{model.Namespace}.{model.TypeName}.g.cs";

        ctx.AddSource(hint, sw.ToString());
    }

    private static void EmitPartialType(IndentedTextWriter writer, TypeModel model)
    {
        var access = model.DeclaredAccessibility?.GetAccessibilityKeyword();

        writer.WriteLine("[global::Il2CppInterop.Common.Attributes.Il2CppType(typeof(Il2CppInternals))]");

        if (model.TypeKind == TypeKind.Struct)
        {
            writer.WriteLine($"{access} partial struct {model.TypeName} :");
            writer.Indent++;
            writer.WriteLine("global::Il2CppSystem.IValueType,");
            writer.WriteLine($"global::Il2CppInterop.Common.IIl2CppType<{model.TypeName}>");
            writer.Indent--;
        }
        else
        {
            writer.WriteLine($"{access} partial {model.TypeKind.GetTypeKeyword()} {model.TypeName} : global::Il2CppInterop.Common.IIl2CppType<{model.TypeName}>");
        }

        writer.WriteLine("{");
        writer.Indent++;

        if (model.TypeKind == TypeKind.Struct)
        {
            EmitValueTypeBridgeMethods(writer);
            writer.WriteLine();
            EmitValueTypeInstanceInterfaceMembers(writer, model);
            writer.WriteLine();
        }
        foreach (var member in model.Members)
        {
            if (member.IsStatic)
                EmitStaticIl2CppProperty(writer, member);
            else if (member.Kind == MemberKind.Il2Cpp)
                EmitIl2CppProperty(writer, member);
            else
                EmitManagedProperty(writer, member);

            writer.WriteLine();
        }
        if (model.NeedsObjectPointerConstructor)
        {
            EmitConstructor(writer, model);
            writer.WriteLine();
        }

        if (model.FinalizerMethodNames.Count > 0 || model.Members.Any(m => m.Kind == MemberKind.Managed))
        {
            EmitFinalizer(writer, model);
            writer.WriteLine();
            EmitLogErrorFinalizer(writer);
            writer.WriteLine();
        }

        EmitStaticInterfaceMembers(writer, model);

        writer.Indent--;
        writer.WriteLine("}");
    }

    #region Value-type-only emit

    private static void EmitValueTypeBridgeMethods(IndentedTextWriter writer)
    {
        writer.WriteLine("[global::Il2CppInterop.Common.Attributes.Il2CppMethod]");
        writer.WriteLine("public global::Il2CppSystem.Boolean Equals(global::Il2CppSystem.IObject obj)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("return this.Equals((object)obj);");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        writer.WriteLine("[global::Il2CppInterop.Common.Attributes.Il2CppMethod(Name = \"GetHashCode\")]");
        writer.WriteLine("public global::Il2CppSystem.Int32 GetIl2CppHashCode()");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("return this.GetHashCode();");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        writer.WriteLine("[global::Il2CppInterop.Common.Attributes.Il2CppMethod(Name = \"ToString\")]");
        writer.WriteLine("public global::Il2CppSystem.String ToIl2CppString()");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("return this.ToString();");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void EmitValueTypeInstanceInterfaceMembers(IndentedTextWriter writer, TypeModel model)
    {
        writer.WriteLine("readonly nint global::Il2CppInterop.Common.IIl2CppType.ObjectClass =>");
        writer.Indent++;
        writer.WriteLine($"global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer;");
        writer.Indent--;
        writer.WriteLine();

        writer.WriteLine("readonly int global::Il2CppSystem.IValueType.Size => Il2CppInternals.Size;");
        writer.WriteLine();

        writer.WriteLine("readonly void global::Il2CppSystem.IValueType.WriteToSpan(global::System.Span<byte> span) =>");
        writer.Indent++;
        writer.WriteLine("global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.WriteToSpan(this, span);");
        writer.Indent--;
    }

    #endregion

    #region Class/interface-only emit

    private static void EmitStaticIl2CppProperty(IndentedTextWriter writer, MemberModel member)
    {
        var accessPrefix = member.Accessibility is { } a ? a.GetAccessibilityKeyword() + " " : "";
        writer.WriteLine($"{accessPrefix}static partial {member.Type} {member.Name}");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"get => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.GetStaticFieldValue<{member.Type}>(Il2CppInternals.FieldInfoPtr_{member.Index});");
        writer.WriteLine($"set => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.SetStaticFieldValue(Il2CppInternals.FieldInfoPtr_{member.Index}, value);");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void EmitIl2CppProperty(IndentedTextWriter writer, MemberModel member)
    {
        var accessPrefix = member.Accessibility is { } a ? a.GetAccessibilityKeyword() + " " : "";
        writer.WriteLine($"{accessPrefix}partial {member.Type} {member.Name}");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"get => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.GetInstanceFieldValue<{member.Type}>(this, Il2CppInternals.FieldOffset_{member.Index});");
        writer.WriteLine($"set => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.SetInstanceFieldValue(this, Il2CppInternals.FieldOffset_{member.Index}, value);");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void EmitManagedProperty(IndentedTextWriter writer, MemberModel member)
    {
        // Backing field
        writer.WriteLine($"[global::Il2CppInterop.Common.Attributes.Il2CppField(Name = nameof({member.Name}))]");
        writer.WriteLine($"private global::Il2CppSystem.IntPtr {member.Name}__BackingField");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"get => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.GetInstanceFieldValue<global::Il2CppSystem.IntPtr>(this, Il2CppInternals.FieldOffset_{member.Index});");
        writer.WriteLine($"set => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.SetInstanceFieldValue(this, Il2CppInternals.FieldOffset_{member.Index}, value);");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine();

        // Public partial property
        var accessPrefix = member.Accessibility is { } a ? a.GetAccessibilityKeyword() + " " : "";
        writer.WriteLine($"{accessPrefix}partial {member.Type} {member.Name}");
        writer.WriteLine("{");
        writer.Indent++;

        writer.WriteLine($"get => global::System.Runtime.InteropServices.GCHandle<{member.Type}>.FromIntPtr({member.Name}__BackingField).Target;");

        writer.WriteLine("set");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"global::System.Runtime.InteropServices.GCHandle<{member.Type}>.FromIntPtr({member.Name}__BackingField).Dispose();");
        writer.WriteLine($"{member.Name}__BackingField = value is not null");
        writer.Indent++;
        writer.WriteLine($"? global::System.Runtime.InteropServices.GCHandle<{member.Type}>.ToIntPtr(new global::System.Runtime.InteropServices.GCHandle<{member.Type}>(value))");
        writer.WriteLine(": default;");
        writer.Indent--;
        writer.Indent--;
        writer.WriteLine("}");

        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void EmitConstructor(IndentedTextWriter writer, TypeModel model)
    {
        writer.WriteLine($"public {model.TypeName}(global::Il2CppInterop.Common.ObjectPointer obj0) : base(obj0)");
        writer.WriteLine("{");
        writer.WriteLine("}");
    }

    private static void EmitFinalizer(IndentedTextWriter writer, TypeModel model)
    {
        writer.WriteLine("[global::Il2CppInterop.Common.Attributes.Il2CppMethod(Name = \"Finalize\")]");
        writer.WriteLine("public override void Il2CppFinalize()");
        writer.WriteLine("{");
        writer.Indent++;

        writer.WriteLine("// This disposal happens when the object is collected by the Il2Cpp GC instead of the managed GC.");
        writer.WriteLine("// That ensures that the delegate is kept alive as long as the Il2Cpp object is alive, even if the managed wrapper gets collected.");
        writer.WriteLine("// In theory, the managed wrapper could be collected and recreated multiple times during the lifetime of the Il2Cpp object,");
        writer.WriteLine("// so this ensures that the managed fields are not disposed prematurely.");

        writer.WriteLine("try");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var name in model.FinalizerMethodNames)
            writer.WriteLine($"this.{name}();");

        foreach (var f in model.Members.Where(f => f.Kind == MemberKind.Managed))
            writer.WriteLine($"{f.Name} = null!;");

        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine("catch (global::System.Exception ex)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("LogErrorIl2CppFinalize(ex);");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine("finally");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("base.Il2CppFinalize(); // Must call base method");
        writer.Indent--;
        writer.WriteLine("}");

        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void EmitLogErrorFinalizer(IndentedTextWriter writer)
    {
        writer.WriteLine("partial void LogErrorIl2CppFinalize(global::System.Exception exception);");
    }

    #endregion

    #region Shared emit

    private static void EmitStaticInterfaceMembers(IndentedTextWriter writer, TypeModel model)
    {
        var tn = model.TypeName;
        var isValueType = model.TypeKind == TypeKind.Struct;

        if (isValueType)
        {
            writer.WriteLine($"static int global::Il2CppInterop.Common.IIl2CppType<{tn}>.Size => Il2CppInternals.Size;");
            writer.WriteLine();

            // ReadFromSpan — constructs via object initializer, one field per offset
            writer.WriteLine($"static {tn} global::Il2CppInterop.Common.IIl2CppType<{tn}>.ReadFromSpan(global::System.ReadOnlySpan<byte> span)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("#nullable disable");
            writer.WriteLine($"return new {tn}");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var member in model.Members.Where(x => !x.IsStatic))
                writer.WriteLine($"{member.Name} = global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.ReadFromSpanAtOffset<{member.Type}>(span, Il2CppInternals.FieldOffset_{member.Index}),");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("#nullable restore");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();

            // WriteToSpan — one WriteToSpanAtOffset call per field
            writer.WriteLine($"static void global::Il2CppInterop.Common.IIl2CppType<{tn}>.WriteToSpan({tn} value, global::System.Span<byte> span)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var member in model.Members.Where(x => !x.IsStatic))
                writer.WriteLine($"global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.WriteToSpanAtOffset(value.{member.Name}, span, Il2CppInternals.FieldOffset_{member.Index});");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }
        else
        {
            writer.WriteLine($"static int global::Il2CppInterop.Common.IIl2CppType<{tn}>.Size => nint.Size;");
            writer.WriteLine();

            writer.WriteLine($"nint global::Il2CppInterop.Common.IIl2CppType.ObjectClass => global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{tn}>.NativeClassPointer;");
            writer.WriteLine();

            writer.WriteLine($"static {tn}? global::Il2CppInterop.Common.IIl2CppType<{tn}>.ReadFromSpan(global::System.ReadOnlySpan<byte> span)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"return global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.ReadReference<{tn}>(span);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();

            writer.WriteLine($"static void global::Il2CppInterop.Common.IIl2CppType<{tn}>.WriteToSpan({tn}? value, global::System.Span<byte> span)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.WriteReference(value, span);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        if (!string.IsNullOrEmpty(model.AssemblyName))
        {
            writer.WriteLine($"static string global::Il2CppInterop.Common.IIl2CppType<{tn}>.AssemblyName => \"{model.AssemblyName}\";");
            writer.WriteLine();
        }

        writer.WriteLine($"static {tn}()");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("global::System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Il2CppInternals).TypeHandle);");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void EmitInternalsClass(IndentedTextWriter writer, TypeModel model)
    {
        var isValueType = model.TypeKind == TypeKind.Struct;

        writer.WriteLine("file static class Il2CppInternals");
        writer.WriteLine("{");
        writer.Indent++;

        if (isValueType)
        {
            writer.WriteLine("public static readonly int Size;");
            foreach (var member in model.Members)
            {
                if (member.IsStatic)
                    writer.WriteLine($"public static readonly nint FieldInfoPtr_{member.Index}; // {member.Name}");
                else
                    writer.WriteLine($"public static readonly int FieldOffset_{member.Index}; // {member.Name}");
            }
        }
        else
        {
            foreach (var member in model.Members)
            {
                if (member.IsStatic)
                    writer.WriteLine($"public static readonly nint FieldInfoPtr_{member.Index}; // {member.Name}");
                else
                    writer.WriteLine($"public static readonly int FieldOffset_{member.Index}; //  {member.Name}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("static Il2CppInternals()");
        writer.WriteLine("{");
        writer.Indent++;

        writer.WriteLine($"global::Il2CppInterop.Runtime.Injection.TypeInjector.RegisterTypeInIl2Cpp<{model.TypeName}>();");

        if (isValueType)
        {
            writer.WriteLine($"Size = global::Il2CppInterop.Runtime.IL2CPP.GetIl2cppValueSize(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer);");

            foreach (var member in model.Members)
            {
                if (member.IsStatic)
                    writer.WriteLine($"FieldInfoPtr_{member.Index} = global::Il2CppInterop.Runtime.IL2CPP.GetIl2CppField(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer, \"{member.Name}\");");
                else
                    writer.WriteLine($"FieldOffset_{member.Index} = (int)global::Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(global::Il2CppInterop.Runtime.IL2CPP.GetIl2CppField(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer, \"{member.Name}\"));");
            };

            writer.WriteLine($"global::Il2CppInterop.Runtime.Runtime.Il2CppObjectPool.RegisterValueTypeInitializer<{model.TypeName}>();");
        }
        else
        {
            foreach (var member in model.Members)
            {
                if (member.IsStatic)
                    writer.WriteLine($"FieldInfoPtr_{member.Index} = global::Il2CppInterop.Runtime.IL2CPP.GetIl2CppField(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer, \"{member.Name}\");");
                else
                    writer.WriteLine($"FieldOffset_{member.Index} = (int)global::Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(global::Il2CppInterop.Runtime.IL2CPP.GetIl2CppField(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer, \"{member.Name}\"));");
            }

            if (!model.IsAbstract)
                writer.WriteLine($"global::Il2CppInterop.Runtime.Runtime.Il2CppObjectPool.RegisterInitializer(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.TypeName}>.NativeClassPointer, ptr => new {model.TypeName}(ptr));");
        }

        writer.Indent--;
        writer.WriteLine("}");

        writer.Indent--;
        writer.WriteLine("}");
    }
    #endregion
}
