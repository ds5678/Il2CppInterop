using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;

namespace Il2CppInterop.Generator;

public sealed class SystemInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "System Interface Implementations";
    public override string Id => "system_interface_implementations";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // Needs to handle:
        // IDisposable
        // IEnumerator and IEnumerator<T>
        // IEnumerable and IEnumerable<T>
        // INotifyCompletion and ICriticalNotifyCompletion
        // IEquatable<T>
        // IComparable and IComparable<T>
        // IReadOnlyCollection<T> and IReadOnlyList<T>
        // IReadOnlySet<T>
        // IReadOnlyDictionary<TKey, TValue>
        // IEqualityComparer<T>
        // IComparer and IComparer<T>

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
        il2CppInterface.InterfaceContexts.Add(systemInterface);

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
            injectedMethod.Overrides.Add(systemMethod);
            il2CppInterface.Methods.Add(injectedMethod);

            injectedMethod.CopyGenericParameters(systemMethod, true);

            var visitor = TypeReplacementVisitor.CreateForMethodCopying(systemMethod, injectedMethod);

            injectedMethod.SetDefaultReturnType(visitor.Replace(systemMethod.ReturnType));

            foreach (var parameter in systemMethod.Parameters)
            {
                injectedMethod.Parameters.Add(new InjectedParameterAnalysisContext(parameter.Name, visitor.Replace(parameter.ParameterType), parameter.Attributes, parameter.ParameterIndex, injectedMethod));
            }

            var il2CppMethod = FindIl2CppMethod(il2CppInterface, systemMethod);

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
            foreach (var parameter in injectedMethod.Parameters)
            {
                instructions.Add(CilOpCodes.Ldarg, parameter);
                // Todo: parameter value conversion
            }
            instructions.Add(CilOpCodes.Call, il2CppMethod);
            // Todo: return value conversion
            instructions.Add(CilOpCodes.Ret);

            injectedMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions
            });
        }
    }

    private static MethodAnalysisContext? FindIl2CppMethod(TypeAnalysisContext il2CppInterface, MethodAnalysisContext systemMethod)
    {
        foreach (var il2CppMethod in il2CppInterface.Methods)
        {
            if (il2CppMethod.Name == systemMethod.Name && !il2CppMethod.IsStatic && il2CppMethod.Parameters.Count == systemMethod.Parameters.Count && il2CppMethod.GenericParameters.Count == systemMethod.GenericParameters.Count)
            {
                if (!AreTypesEqual(il2CppMethod.ReturnType, systemMethod.ReturnType))
                {
                    continue;
                }

                var parametersMatch = true;
                for (var i = 0; i < il2CppMethod.Parameters.Count; i++)
                {
                    var il2CppParameterType = il2CppMethod.Parameters[i].ParameterType;
                    var systemParameterType = systemMethod.Parameters[i].ParameterType;
                    if (!AreTypesEqual(il2CppParameterType, systemParameterType))
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

        static bool AreTypesEqual(TypeAnalysisContext a, TypeAnalysisContext b)
        {
            if (a is GenericParameterTypeAnalysisContext aGeneric && b is GenericParameterTypeAnalysisContext bGeneric)
            {
                // For generic parameters, we consider them equal if they are in the same position
                return aGeneric.Type == bGeneric.Type && aGeneric.Index == bGeneric.Index;
            }
            return TypeAnalysisContextEqualityComparer.Instance.Equals(a, b);
        }
    }
}
