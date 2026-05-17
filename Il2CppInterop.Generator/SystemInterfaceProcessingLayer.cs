using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Conversions;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;

namespace Il2CppInterop.Generator;

/// <summary>
/// This processing layer finds matching interfaces in mscorlib and Il2Cppmscorlib and implements
/// the mscorlib interfaces on the Il2Cppmscorlib interfaces, forwarding calls to the existing Il2Cppmscorlib methods.
/// This allows user code to use the normal .NET interfaces and have them work with the Il2Cpp types.
/// </summary>
public sealed class SystemInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "System Interface Implementations";
    public override string Id => "system_interface_implementations";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var pairs = FindPairs(appContext);

        foreach (var (il2CppInterface, systemInterface) in pairs)
        {
            ImplementInterface(il2CppInterface, systemInterface);
        }
    }

    private static List<(TypeAnalysisContext, TypeAnalysisContext)> FindPairs(ApplicationAnalysisContext appContext)
    {
        List<(TypeAnalysisContext, TypeAnalysisContext)> pairs = [];
        foreach (var type in appContext.Il2CppMscorlib.Types)
        {
            if (type.IsInterface)
            {
                var systemType = appContext.Mscorlib.GetTypeByFullName(type.DefaultFullName);
                if (systemType != null && IsPublic(systemType))
                {
                    pairs.Add((type, systemType));
                }
            }
        }

        return pairs;

        static bool IsPublic(TypeAnalysisContext? type)
        {
            var current = type;
            while (current is not null)
            {
                if (current.Visibility is not TypeAttributes.Public and not TypeAttributes.NestedPublic)
                {
                    return false;
                }
                current = current.DeclaringType;
            }
            return true;
        }
    }

    private static void ImplementInterface(TypeAnalysisContext il2CppInterface, TypeAnalysisContext systemInterface)
    {
        il2CppInterface.InterfaceContexts.Add(systemInterface.MaybeMakeGenericInstanceType(il2CppInterface.GenericParameters));

        foreach (var systemMethod in systemInterface.Methods)
        {
            if (systemMethod.IsStatic)
            {
                continue;
            }

            var attributes = (systemMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.MemberAccessMask)) | MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.NewSlot;
            var injectedMethod = new InjectedMethodAnalysisContext(il2CppInterface, $"{systemInterface.FullName}.{systemMethod.Name}", systemMethod.ReturnType, attributes, [])
            {
                IsInjected = true,
            };
            il2CppInterface.Methods.Add(injectedMethod);

            injectedMethod.CopyGenericParameters(systemMethod, true);

            var visitor = TypeReplacementVisitor.CreateForMethodCopying(systemMethod, injectedMethod);

            injectedMethod.SetDefaultReturnType(visitor.Replace(systemMethod.ReturnType));

            foreach (var parameter in systemMethod.Parameters)
            {
                injectedMethod.Parameters.Add(new InjectedParameterAnalysisContext(parameter.Name, visitor.Replace(parameter.ParameterType), parameter.Attributes, parameter.ParameterIndex, injectedMethod));
            }

            injectedMethod.Overrides.Add(systemMethod.MaybeMakeConcreteGeneric(il2CppInterface.GenericParameters, injectedMethod.GenericParameters));

            var il2CppMethod = FindIl2CppMethod(il2CppInterface, systemMethod.Name, injectedMethod, out var returnConversion, out var parameterConversions);

            if (il2CppMethod is null)
            {
                injectedMethod.PutExtraData(new NativeMethodBody()
                {
                    Instructions =
                    [
                        new Instruction(CilOpCodes.Ldnull),
                        new Instruction(CilOpCodes.Throw),
                    ]
                });
                continue;
            }

            List<Instruction> instructions = [];
            instructions.Add(CilOpCodes.Ldarg_0);
            for (var i = 0; i < injectedMethod.Parameters.Count; i++)
            {
                var parameter = injectedMethod.Parameters[i];
                instructions.Add(CilOpCodes.Ldarg, parameter);
                parameterConversions[i].Add(instructions);
            }
            instructions.Add(CilOpCodes.Callvirt, il2CppMethod.MaybeMakeConcreteGeneric(il2CppInterface.GenericParameters, injectedMethod.GenericParameters));
            returnConversion.Add(instructions);
            instructions.Add(CilOpCodes.Ret);

            injectedMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions
            });
        }
    }

    private static MethodAnalysisContext? FindIl2CppMethod(TypeAnalysisContext il2CppInterface, string methodName, MethodAnalysisContext injectedMethod, out Conversion returnConversion, out Conversion[] parameterConversions)
    {
        returnConversion = NullConversion.Instance;
        parameterConversions = new Conversion[injectedMethod.Parameters.Count];

        // Search in reverse order because the most user-friendly overloads will be last (they always get added to the end of the list).
        // This ensures correct behavior for array parameters, which have special handling in the user-friendly overload processing layer.
        for (var il2CppMethodIndex = il2CppInterface.Methods.Count - 1; il2CppMethodIndex >= 0; il2CppMethodIndex--)
        {
            var il2CppMethod = il2CppInterface.Methods[il2CppMethodIndex];
            if (il2CppMethod.Name == methodName &&
                !il2CppMethod.IsStatic &&
                il2CppMethod.Parameters.Count == injectedMethod.Parameters.Count &&
                il2CppMethod.GenericParameters.Count == injectedMethod.GenericParameters.Count &&
                il2CppMethod.IsVoid == injectedMethod.IsVoid)
            {
                var visitor = TypeReplacementVisitor.CreateForMethodCopying(il2CppMethod, injectedMethod);
                if (!AreTypesEqual(visitor.Replace(il2CppMethod.ReturnType), injectedMethod.ReturnType, out returnConversion))
                {
                    continue;
                }

                var parametersMatch = true;
                for (var i = 0; i < il2CppMethod.Parameters.Count; i++)
                {
                    var injectedParameterType = injectedMethod.Parameters[i].ParameterType;
                    var il2CppParameterType = visitor.Replace(il2CppMethod.Parameters[i].ParameterType);
                    if (!AreTypesEqual(injectedParameterType, il2CppParameterType, out parameterConversions[i]))
                    {
                        parametersMatch = false;
                        break;
                    }
                }
                if (parametersMatch)
                {
                    return il2CppMethod;
                }
            }
        }
        return null;

        static bool AreTypesEqual(TypeAnalysisContext from, TypeAnalysisContext to, out Conversion conversion)
        {
            if (TypeAnalysisContextEqualityComparer.Instance.Equals(from, to))
            {
                conversion = NullConversion.Instance;
                return true;
            }
            else if (from.TryGetConversionTo(to, out var conversionMethod) || to.TryGetConversionFrom(from, out conversionMethod))
            {
                // An implicit or explicit conversion exists
                conversion = new MethodCallConversion(conversionMethod);
                return true;
            }
            else if (to.KnownType is KnownTypeCode.System_Object && !from.IsValueType)
            {
                // Any reference type can be converted to System.Object
                conversion = NullConversion.Instance;
                return true;
            }
            else if (to.KnownType is KnownTypeCode.Il2CppSystem_IObject && from.KnownType is KnownTypeCode.System_Object)
            {
                // obj as IObject
                conversion = new IsInstanceConversion(to);
                return true;
            }
            else if (to.IsInterface && from.IsInterface && to.DefaultFullName == from.DefaultFullName)
            {
                if (to.Namespace.StartsWith("Il2Cpp", StringComparison.Ordinal))
                {
                    // System.IX to Il2CppSystem.IX
                    // Need to cast
                    conversion = new CastClassConversion(to);
                    return true;
                }
                else if (from.Namespace.StartsWith("Il2Cpp", StringComparison.Ordinal))
                {
                    // Il2CppSystem.IX to System.IX
                    // Since the Il2Cpp interface implements the System interface, no conversion is needed.
                    conversion = NullConversion.Instance;
                    return true;
                }
                else
                {
                    Debug.Fail($"Unexpected case where two reference types have the same full name but are not considered equal: {from.FullName} and {to.FullName}");
                    conversion = NullConversion.Instance;
                    return false;
                }
            }
            else
            {
                conversion = NullConversion.Instance;
                return false;
            }
        }
    }
}
