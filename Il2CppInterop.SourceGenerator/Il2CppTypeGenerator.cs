using System.CodeDom.Compiler;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Il2CppInterop.SourceGenerator
{
    [Generator]
    public sealed class Il2CppTypeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var types = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "Il2CppInterop.Common.Attributes.InjectedTypeAttribute",
                    predicate: static (node, _) =>
                        node is (ClassDeclarationSyntax or InterfaceDeclarationSyntax) &&
                        ((TypeDeclarationSyntax)node).Modifiers.Any(SyntaxKind.PartialKeyword),
                    transform: static (ctx, ct) => GetClassModel(ctx, ct))
                .Where(static m => m is not null);

            context.RegisterSourceOutput(types, static (ctx, model) => Emit(ctx, model!));
        }

        private static ClassModel? GetClassModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
            if (ctx.TargetNode is not (ClassDeclarationSyntax or InterfaceDeclarationSyntax)) return null;
            if (type is { TypeKind: TypeKind.Class, BaseType.SpecialType: SpecialType.System_Object })
                return null;
            var injectedTypeAttr = ctx.Attributes[0];
            var assemblyName = injectedTypeAttr.NamedArguments
                .FirstOrDefault(a => a.Key == "Assembly")
                .Value.Value as string;

            var finalizerMethod = type.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.GetAttributes()
                    .Any(a => a.AttributeClass?.Name == "Il2CppFinalizerAttribute"));

            var fields = ImmutableArray.CreateBuilder<FieldModel>();
            int index = 0;

            foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
            {
                ct.ThrowIfCancellationRequested();

                var attrs = member.GetAttributes();
                var il2cpp = attrs.Any(a => a.AttributeClass?.Name == "Il2CppFieldAttribute");
                var managed = attrs.Any(a => a.AttributeClass?.Name == "ManagedFieldAttribute");

                if (!il2cpp && !managed) continue;
                if (!member.IsPartialDefinition) continue;
                if (managed && member.IsStatic) continue; // invalid combo — IL2CPP002 covers this

                // DeclaredAccessibility defaults to Private when no modifier is written,
                // so we check the syntax directly to distinguish explicit "private" from omitted.
                var explicitAccess = member.DeclaringSyntaxReferences
                    .Select(r => r.GetSyntax(ct))
                    .OfType<PropertyDeclarationSyntax>()
                    .SelectMany(s => s.Modifiers)
                    .FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword)
                                         || m.IsKind(SyntaxKind.InternalKeyword)
                                         || m.IsKind(SyntaxKind.PrivateKeyword)
                                         || m.IsKind(SyntaxKind.ProtectedKeyword));

                fields.Add(new FieldModel(
                    PropertyName: member.Name,
                    PropertyType: member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Kind: managed ? FieldKind.Managed : FieldKind.Il2Cpp,
                    Index: index++,
                    Accessibility: explicitAccess == default ? null : member.DeclaredAccessibility,
                    IsStatic: member.IsStatic
                ));
            }

            var hasObjectPointerCtor = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.MethodKind == MethodKind.Constructor &&
                          m.Parameters.Length == 1 &&
                          m.Parameters[0].Type.ToDisplayString() == "Il2CppInterop.Common.ObjectPointer");

            return new ClassModel(
                Namespace: type.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : type.ContainingNamespace.ToDisplayString(),
                ClassName: type.Name,
                AssemblyName: assemblyName,
                Fields: fields.ToImmutable(),
                FinalizerMethodName: finalizerMethod?.Name,
                HasObjectPointerCtor: hasObjectPointerCtor,
                ClassAccessibility: type.DeclaredAccessibility,
                IsAbstract: type.IsAbstract,
                IsInterface: type.TypeKind == TypeKind.Interface
            );
    }

        // Converts a Roslyn Accessibility to the keyword(s) needed in emitted source.
        // Protected/ProtectedAndInternal are included for completeness but are unusual
        // on a top-level injected type.
        private static string AccessibilityKeyword(Accessibility a) => a switch
        {
            Accessibility.Public              => "public",
            Accessibility.Internal            => "internal",
            Accessibility.Private             => "private",
            Accessibility.Protected           => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _                                 => "public",
        };

        private static void Emit(SourceProductionContext ctx, ClassModel model)
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

            EmitPartialClass(writer, model);
            writer.WriteLine();
            EmitInternalsClass(writer, model);

            if (model.Namespace is not null)
            {
                writer.Indent--;
                writer.WriteLine("}");
            }

            var hint = model.Namespace is null
                ? $"{model.ClassName}.g.cs"
                : $"{model.Namespace}.{model.ClassName}.g.cs";

            ctx.AddSource(hint, sw.ToString());
        }

        private static void EmitPartialClass(IndentedTextWriter writer, ClassModel model)
        {
            var classAccess = AccessibilityKeyword(model.ClassAccessibility);
            writer.WriteLine($"[global::Il2CppInterop.Common.Attributes.Il2CppType(typeof(Il2CppInternals))]");
            var typeKindKeyword = model.IsInterface ? "interface" : "class";
            writer.WriteLine($"{classAccess} partial {typeKindKeyword} {model.ClassName} : global::Il2CppInterop.Common.IIl2CppType<{model.ClassName}>");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var field in model.Fields)
            {
                if (field.IsStatic)
                    EmitStaticIl2CppProperty(writer, field);
                else if (field.Kind == FieldKind.Il2Cpp)
                    EmitIl2CppProperty(writer, field);
                else
                    EmitManagedProperty(writer, field);

                writer.WriteLine();
            }

            if (!model.IsInterface)
            {
                if (!model.HasObjectPointerCtor)
                {
                    EmitConstructor(writer, model);
                    writer.WriteLine();
                }

                EmitFinalizer(writer, model);
                writer.WriteLine();
                EmitLogErrorFinalizer(writer);
                writer.WriteLine();
            }
            EmitIl2CppTypeMembers(writer, model);

            writer.Indent--;
            writer.WriteLine("}");
        }


        private static void EmitStaticIl2CppProperty(IndentedTextWriter writer, FieldModel field)
        {
            var accessPrefix = field.Accessibility is { } a ? AccessibilityKeyword(a) + " " : "";
            writer.WriteLine($"{accessPrefix}static partial {field.PropertyType} {field.PropertyName}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"get => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.GetStaticFieldValue<{field.PropertyType}>(Il2CppInternals.FieldInfoPtr_{field.Index});");
            writer.WriteLine($"set => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.SetStaticFieldValue(Il2CppInternals.FieldInfoPtr_{field.Index}, value);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void EmitIl2CppProperty(IndentedTextWriter writer, FieldModel field)
        {
            var accessPrefix = field.Accessibility is { } a ? AccessibilityKeyword(a) + " " : "";
            writer.WriteLine($"{accessPrefix}partial {field.PropertyType} {field.PropertyName}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"get => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.GetInstanceFieldValue<{field.PropertyType}>(this, Il2CppInternals.FieldOffset_{field.Index});");
            writer.WriteLine($"set => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.SetInstanceFieldValue(this, Il2CppInternals.FieldOffset_{field.Index}, value);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void EmitManagedProperty(IndentedTextWriter writer, FieldModel field)
        {
            // Backing field
            writer.WriteLine($"[global::Il2CppInterop.Common.Attributes.Il2CppField(Name = nameof({field.PropertyName}))]");
            writer.WriteLine($"private global::Il2CppSystem.IntPtr {field.PropertyName}__BackingField");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"get => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.GetInstanceFieldValue<global::Il2CppSystem.IntPtr>(this, Il2CppInternals.FieldOffset_{field.Index});");
            writer.WriteLine($"set => global::Il2CppInterop.Runtime.InteropTypes.FieldAccess.SetInstanceFieldValue(this, Il2CppInternals.FieldOffset_{field.Index}, value);");
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();

            // Public partial property
            var accessPrefix = field.Accessibility is { } a ? AccessibilityKeyword(a) + " " : "";
            writer.WriteLine($"{accessPrefix}partial {field.PropertyType} {field.PropertyName}");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"get => global::System.Runtime.InteropServices.GCHandle<{field.PropertyType}>.FromIntPtr({field.PropertyName}__BackingField).Target;");

            writer.WriteLine("set");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"global::System.Runtime.InteropServices.GCHandle<{field.PropertyType}>.FromIntPtr({field.PropertyName}__BackingField).Dispose();");
            writer.WriteLine($"{field.PropertyName}__BackingField = value is not null");
            writer.Indent++;
            writer.WriteLine($"? global::System.Runtime.InteropServices.GCHandle<{field.PropertyType}>.ToIntPtr(new global::System.Runtime.InteropServices.GCHandle<{field.PropertyType}>(value))");
            writer.WriteLine(": default;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void EmitConstructor(IndentedTextWriter writer, ClassModel model)
        {
            var access = AccessibilityKeyword(model.ClassAccessibility);
            writer.WriteLine($"{access} {model.ClassName}(global::Il2CppInterop.Common.ObjectPointer obj0) : base(obj0)");
            writer.WriteLine("{");
            writer.WriteLine("}");
        }

        private static void EmitFinalizer(IndentedTextWriter writer, ClassModel model)
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

            if (model.FinalizerMethodName is not null)
                writer.WriteLine($"this.{model.FinalizerMethodName}();");

            foreach (var f in model.Fields.Where(f => f.Kind == FieldKind.Managed))
                writer.WriteLine($"{f.PropertyName} = null!;");

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

        private static void EmitIl2CppTypeMembers(IndentedTextWriter writer, ClassModel model)
        {
            var cn = model.ClassName;

            writer.WriteLine($"static int global::Il2CppInterop.Common.IIl2CppType<{cn}>.Size => nint.Size;");
            writer.WriteLine();

            writer.WriteLine($"nint global::Il2CppInterop.Common.IIl2CppType.ObjectClass => global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{cn}>.NativeClassPointer;");
            writer.WriteLine();

            writer.WriteLine($"static {cn}? global::Il2CppInterop.Common.IIl2CppType<{cn}>.ReadFromSpan(global::System.ReadOnlySpan<byte> span)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"return global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.ReadReference<{cn}>(span);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();

            writer.WriteLine($"static void global::Il2CppInterop.Common.IIl2CppType<{cn}>.WriteToSpan({cn}? value, global::System.Span<byte> span)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"global::Il2CppInterop.Runtime.InteropTypes.Il2CppType.WriteReference(value, span);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();

            if (!string.IsNullOrEmpty(model.AssemblyName))
            {
                writer.WriteLine($"static string global::Il2CppInterop.Common.IIl2CppType<{cn}>.AssemblyName => \"{model.AssemblyName}\";");
                writer.WriteLine();
            }


            writer.WriteLine($"static {cn}()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("global::System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Il2CppInternals).TypeHandle);");
            writer.Indent--;
            writer.WriteLine("}");
        }
        private static void EmitInternalsClass(IndentedTextWriter writer, ClassModel model)
        {
            writer.WriteLine("file static class Il2CppInternals");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var field in model.Fields)
            {
                if (field.IsStatic)
                    writer.WriteLine($"public static readonly nint FieldInfoPtr_{field.Index};");
                else
                    writer.WriteLine($"public static readonly int FieldOffset_{field.Index};");
            }
            // writer.WriteLine();
            writer.WriteLine("static Il2CppInternals()");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"global::Il2CppInterop.Runtime.Injection.TypeInjector.RegisterTypeInIl2Cpp<{model.ClassName}>();");

            foreach (var field in model.Fields)
            {
                if (field.IsStatic)
                    writer.WriteLine($"FieldInfoPtr_{field.Index} = global::Il2CppInterop.Runtime.IL2CPP.GetIl2CppField(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.ClassName}>.NativeClassPointer, \"{field.PropertyName}\");");
                else
                    writer.WriteLine($"FieldOffset_{field.Index} = (int)global::Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(global::Il2CppInterop.Runtime.IL2CPP.GetIl2CppField(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.ClassName}>.NativeClassPointer, \"{field.PropertyName}\"));");
            }

            if (!model.IsAbstract)
                writer.WriteLine($"global::Il2CppInterop.Runtime.Runtime.Il2CppObjectPool.RegisterInitializer(global::Il2CppInterop.Runtime.Il2CppClassPointerStore<{model.ClassName}>.NativeClassPointer, ptr => new {model.ClassName}(ptr));");

            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
        }
    }
    sealed record ClassModel(
        string? Namespace,
        string ClassName,
        string? AssemblyName,
        ImmutableArray<FieldModel> Fields,
        string? FinalizerMethodName,
        bool HasObjectPointerCtor,
        Accessibility ClassAccessibility,
        bool IsAbstract,
        bool IsInterface
    );

    sealed record FieldModel(
        string PropertyName,
        string PropertyType,
        FieldKind Kind,
        int Index,
        Accessibility? Accessibility,
        bool IsStatic
    );

    enum FieldKind { Il2Cpp, Managed }
}
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
