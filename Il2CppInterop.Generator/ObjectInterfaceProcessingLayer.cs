using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class ObjectInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Object Interface Processor";
    public override string Id => "object_interface_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // Find references
        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemValueType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");
        var il2CppSystemEnum = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");

        // Inject interfaces
        var il2CppSystemIObject = InjectInterface(appContext, "IObject");
        var il2CppSystemIValueType = InjectInterface(appContext, "IValueType");
        var il2CppSystemIEnum = InjectInterface(appContext, "IEnum");

        // Set KnownType
        il2CppSystemIObject.KnownType = KnownTypeCode.Il2CppSystem_IObject;
        il2CppSystemIValueType.KnownType = KnownTypeCode.Il2CppSystem_IValueType;
        il2CppSystemIEnum.KnownType = KnownTypeCode.Il2CppSystem_IEnum;

        // Il2CppSystem.IValueType : Il2CppSystem.IObject
        il2CppSystemIValueType.InterfaceContexts.Add(il2CppSystemIObject);

        // Il2CppSystem.IEnum : Il2CppSystem.IObject, Il2CppSystem.IValueType /* and the other interfaces that Il2CppSystem.Enum implements */
        il2CppSystemIEnum.InterfaceContexts.Add(il2CppSystemIObject);
        il2CppSystemIEnum.InterfaceContexts.Add(il2CppSystemIValueType);
        foreach (var interfaceContext in il2CppSystemEnum.InterfaceContexts)
        {
            il2CppSystemIEnum.InterfaceContexts.Add(interfaceContext);
        }

        // Il2CppSystem.ValueType : Il2CppSystem.IValueType
        il2CppSystemValueType.InterfaceContexts.Add(il2CppSystemIValueType);

        // Il2CppSystem.Enum : Il2CppSystem.IEnum
        il2CppSystemEnum.InterfaceContexts.Add(il2CppSystemIEnum);

        // Add methods to interfaces
        foreach (var method in il2CppSystemObject.Methods)
        {
            if (ShouldCreateInterfaceMethod(method))
            {
                CreateInterfaceMethod(method, il2CppSystemObject, il2CppSystemIObject);
            }
        }
        foreach (var method in il2CppSystemValueType.Methods)
        {
            if (ShouldCreateInterfaceMethod(method))
            {
                CreateInterfaceMethod(method, il2CppSystemValueType, il2CppSystemIValueType);
            }
        }
        foreach (var method in il2CppSystemEnum.Methods)
        {
            if (ShouldCreateInterfaceMethod(method))
            {
                CreateInterfaceMethod(method, il2CppSystemEnum, il2CppSystemIEnum);
            }
        }

        OverrideBaseInterfaceMethods(il2CppSystemValueType, il2CppSystemIValueType, [il2CppSystemObject]);
        OverrideBaseInterfaceMethods(il2CppSystemEnum, il2CppSystemIEnum, [il2CppSystemValueType, il2CppSystemObject]);

        // Add interfaces to types
        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                type.InterfaceContexts.Add(il2CppSystemIObject);

                if (type.DefaultBaseType == il2CppSystemValueType)
                {
                    type.InterfaceContexts.Add(il2CppSystemIValueType);
                }
                else if (type.DefaultBaseType == il2CppSystemEnum)
                {
                    type.InterfaceContexts.Add(il2CppSystemIValueType);
                    type.InterfaceContexts.Add(il2CppSystemIEnum);
                }
            }
        }

        // Implement static constructors for interfaces to set class pointers
        ImplementStaticConstructor(il2CppSystemObject, il2CppSystemIObject);
        ImplementStaticConstructor(il2CppSystemValueType, il2CppSystemIValueType);
        ImplementStaticConstructor(il2CppSystemEnum, il2CppSystemIEnum);
    }

    private static bool ShouldCreateInterfaceMethod(MethodAnalysisContext method)
    {
        if (method.IsConstructor || method.IsStatic || method.IsInjected)
        {
            return false;
        }
        if (method.DefaultName == "Finalize")
        {
            // Only reference types can have a Finalize method, and it can't be called, except by derived finalizers,
            // so we can safely exclude it from the interface.
            return false;
        }
        return true;
    }

    private static InjectedTypeAnalysisContext InjectInterface(ApplicationAnalysisContext appContext, string name)
    {
        var result = appContext.Il2CppMscorlib.InjectType("Il2CppSystem", name, null, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        result.IsInjected = true;
        return result;
    }

    private static void CreateInterfaceMethod(MethodAnalysisContext classMethod, TypeAnalysisContext classType, TypeAnalysisContext interfaceType)
    {
        Debug.Assert(classMethod.DeclaringType == classType);
        Debug.Assert(classMethod.GenericParameters.Count == 0);
        Debug.Assert(classType.GenericParameters.Count == 0);
        Debug.Assert(interfaceType.IsInjected);

        var @explicitlyImplements = classMethod.DefaultName.Contains('.');

        var interfaceMethod = new InjectedMethodAnalysisContext(
            interfaceType,
            classMethod.DefaultName,
            classMethod.ReturnType,
            explicitlyImplements
                ? MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final
                : MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            classMethod.Parameters.Select(p => p.ParameterType).ToArray(),
            classMethod.Parameters.Select(p => p.Name).ToArray(),
            classMethod.Parameters.Select(p => p.Attributes).ToArray())
        {
            OverrideName = classMethod.OverrideName,
        };
        interfaceType.Methods.Add(interfaceMethod);

        foreach (var @override in classMethod.Overrides)
        {
            if (@override.DeclaringType is not { IsInterface: true })
            {
                continue;
            }
            if (@explicitlyImplements)
            {
                interfaceMethod.Overrides.Add(@override);
                @explicitlyImplements = true;
                continue;
            }

            var overrideImplementation = new InjectedMethodAnalysisContext(
                interfaceType,
                $"{@override.DeclaringType!.FullName}.{interfaceMethod.Name}",
                classMethod.ReturnType,
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final,
                classMethod.Parameters.Select(p => p.ParameterType).ToArray(),
                classMethod.Parameters.Select(p => p.Name).ToArray(),
                classMethod.Parameters.Select(p => p.Attributes).ToArray())
            {
                OverrideName = classMethod.OverrideName,
                IsInjected = true,
            };
            interfaceType.Methods.Add(overrideImplementation);
            overrideImplementation.Overrides.Add(@override);

            List<Instruction> instructions =
            [
                new Instruction(CilOpCodes.Ldarg_0),
                ..Enumerable.Range(0, classMethod.Parameters.Count).Select(i => new Instruction
                {
                    Code = CilOpCodes.Ldarg,
                    Operand = overrideImplementation.Parameters[i],
                }),
                new Instruction(CilOpCodes.Callvirt, interfaceMethod),
                new Instruction(CilOpCodes.Ret),
            ];
            overrideImplementation.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions,
            });
        }

        classMethod.InterfaceRedirectMethod = interfaceMethod;
        if (!classMethod.IsVirtual)
        {
            // If the class method isn't virtual, we want to make sure it can't be overridden in subclasses.
            classMethod.Attributes |= MethodAttributes.Final | MethodAttributes.NewSlot;
        }
        classMethod.Attributes |= MethodAttributes.Virtual | MethodAttributes.HideBySig;
    }

    /// <summary>
    /// Makes the methods of the class's interface also override those of the interfaces of its base types, whose methods are new slots,
    /// so that a call through <c>Il2CppSystem.IObject</c> reaches the class's own override, such as that of <c>ValueType.Equals</c>.
    /// </summary>
    private static void OverrideBaseInterfaceMethods(TypeAnalysisContext classType, TypeAnalysisContext interfaceType, TypeAnalysisContext[] baseTypes)
    {
        foreach (var classMethod in classType.Methods)
        {
            if (classMethod.InterfaceRedirectMethod is not { } interfaceMethod
                || !classMethod.DefaultAttributes.HasFlag(MethodAttributes.Virtual)
                || classMethod.DefaultAttributes.HasFlag(MethodAttributes.NewSlot))
            {
                continue;
            }

            foreach (var baseType in baseTypes)
            {
                var baseInterfaceMethod = baseType.Methods
                    .FirstOrDefault(m => m.DefaultName == classMethod.DefaultName && HasSameParameters(m, classMethod))
                    ?.InterfaceRedirectMethod;
                if (baseInterfaceMethod is not null)
                {
                    AddForwardingOverride(interfaceType, baseInterfaceMethod, interfaceMethod);
                }
            }
        }
    }

    private static bool HasSameParameters(MethodAnalysisContext first, MethodAnalysisContext second)
    {
        return first.Parameters.Count == second.Parameters.Count
            && first.Parameters.Zip(second.Parameters).All(pair => pair.First.ParameterType == pair.Second.ParameterType);
    }

    /// <summary>Overrides the method of the base interface with one that calls the method of the interface.</summary>
    private static void AddForwardingOverride(TypeAnalysisContext interfaceType, MethodAnalysisContext baseInterfaceMethod, MethodAnalysisContext interfaceMethod)
    {
        var overrideImplementation = new InjectedMethodAnalysisContext(
            interfaceType,
            $"{baseInterfaceMethod.DeclaringType!.FullName}.{baseInterfaceMethod.Name}",
            interfaceMethod.ReturnType,
            MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final,
            interfaceMethod.Parameters.Select(p => p.ParameterType).ToArray(),
            interfaceMethod.Parameters.Select(p => p.Name).ToArray(),
            interfaceMethod.Parameters.Select(p => p.Attributes).ToArray())
        {
            IsInjected = true,
        };
        interfaceType.Methods.Add(overrideImplementation);
        overrideImplementation.Overrides.Add(baseInterfaceMethod);

        List<Instruction> instructions =
        [
            new Instruction(CilOpCodes.Ldarg_0),
            ..overrideImplementation.Parameters.Select(parameter => new Instruction(CilOpCodes.Ldarg, parameter)),
            new Instruction(CilOpCodes.Callvirt, interfaceMethod),
            new Instruction(CilOpCodes.Ret),
        ];
        overrideImplementation.PutExtraData(new NativeMethodBody()
        {
            Instructions = instructions,
        });
    }

    private static void ImplementStaticConstructor(TypeAnalysisContext classType, TypeAnalysisContext interfaceType)
    {
        // Set the class pointer for the interface to the class pointer of the class
        var il2CppType = interfaceType.AppContext.ResolveTypeOrThrow(typeof(Il2CppType));
        var getClassPointerMethod = il2CppType.Methods.Single(m => m.Name == nameof(Il2CppType.GetClassPointer) && m.GenericParameters.Count == 1);
        var setClassPointerMethod = il2CppType.Methods.Single(m => m.Name == nameof(Il2CppType.SetClassPointer) && m.GenericParameters.Count == 1);

        var cctor = new InjectedMethodAnalysisContext(
            interfaceType,
            ".cctor",
            interfaceType.AppContext.SystemTypes.SystemVoidType,
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            [])
        {
            IsInjected = true,
        };
        interfaceType.Methods.Add(cctor);
        cctor.PutExtraData(new NativeMethodBody()
        {
            Instructions =
            [
                new Instruction(CilOpCodes.Call, getClassPointerMethod.MakeGenericInstanceMethod(classType)),
                new Instruction(CilOpCodes.Call, setClassPointerMethod.MakeGenericInstanceMethod(interfaceType)),
                new Instruction(CilOpCodes.Ret),
            ],
        });
    }
}
