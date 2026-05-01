using CppAst;
using Il2CppInterop.StructGenerator.Resources;

namespace Il2CppInterop.StructGenerator.Utilities;

internal static class ConversionUtils
{
    private static readonly Dictionary<string, string> STypeRenames = new()
    {
        ["Il2CppType"] = "Il2CppTypeStruct",
        ["FieldInfo"] = "Il2CppFieldInfo",
        ["MethodInfo"] = "Il2CppMethodInfo",
        ["PropertyInfo"] = "Il2CppPropertyInfo",
        ["ParameterInfo"] = "Il2CppParameterInfo",
        ["EventInfo"] = "Il2CppEventInfo"
    };

    private static readonly Dictionary<string, string> STypeConversions = new()
    {
        // Not stubbed
        ["Il2CppArray"] = "void",
        ["Il2CppGenericClass"] = "void",
        ["Il2CppTypeDefinition"] = "void",
        ["Il2CppInteropData"] = "void",
        ["Il2CppRGCTXData"] = "void",
        ["Il2CppCodeGenModule"] = "void",
        ["Il2CppNameToTypeDefinitionIndexHashTable"] = "void",
        ["Il2CppNameToTypeHandleHashTable"] = "void",

        ["methodPointerType"] = "void*",
        ["Il2CppMethodPointer"] = "void*",
        ["InvokerMethod"] = "void*",

        ["TypeIndex"] = "int",
        ["TypeDefinitionIndex"] = "int",
        ["FieldIndex"] = "int",
        ["DefaultValueIndex"] = "int",
        ["DefaultValueDataIndex"] = "int",
        ["CustomAttributeIndex"] = "int",
        ["ParameterIndex"] = "int",
        ["MethodIndex"] = "int",
        ["GenericMethodIndex"] = "int",
        ["PropertyIndex"] = "int",
        ["EventIndex"] = "int",
        ["GenericContainerIndex"] = "int",
        ["GenericParameterIndex"] = "int",
        ["GenericParameterConstraintIndex"] = "short",
        ["NestedTypeIndex"] = "int",
        ["InterfacesIndex"] = "int",
        ["VTableIndex"] = "int",
        ["InterfaceOffsetIndex"] = "int",
        ["RGCTXIndex"] = "int",
        ["StringIndex"] = "int",
        ["StringLiteralIndex"] = "int",
        ["GenericInstIndex"] = "int",
        ["ImageIndex"] = "int",
        ["AssemblyIndex"] = "int",
        ["InteropDataIndex"] = "int",

        ["char"] = "byte",
        ["int8_t"] = "sbyte",
        ["uint8_t"] = "byte",
        ["int16_t"] = "short",
        ["uint16_t"] = "ushort",
        ["int32_t"] = "int",
        ["uint32_t"] = "uint",
        ["int64_t"] = "long",
        ["uint64_t"] = "ulong",
        ["intptr_t"] = "IntPtr",
        ["uintptr_t"] = "UIntPtr",
        ["size_t"] = "IntPtr",

        ["unsigned int"] = "uint",
        ["unsigned long"] = "UIntPtr",
        ["unsigned long long"] = "ulong",
    };

    private static readonly string[] SInvalidNames =
    {
        "object",
        "class",
        "struct",
        "base"
    };

    public static string GetName(CppField field)
    {
        var name = field.Name;
        if (SInvalidNames.Contains(name))
        {
            return $"_{name}";
        }
        else if (name.Length == 0)
        {
            if (field.Parent is CppClass { Name: "Il2CppMethodInfo" } && field.Type is CppClass { ClassKind: CppClassKind.Union } unionType)
            {
                // IlCppMethodInfo has two unnamed union fields
                if (unionType.Fields.Any(f => f.Name is "rgctx_data"))
                {
                    return "runtime_data";
                }
                if (unionType.Fields.Any(f => f.Name is "genericMethod"))
                {
                    return "generic_data";
                }
            }
            throw new ArgumentException("Field has no name and is not part of a known union", nameof(field));
        }
        else
        {
            return name;
        }
    }

    public static string CppTypeToCSharpName(CppType type, out bool needsImport)
    {
        needsImport = false;

        if (type is CppArrayType arrayType)
        {
            return arrayType.SizeOf switch
            {
                0 => "",
                4 => "uint",
                8 => "ulong",
                _ => $"{CppTypeToCSharpName(arrayType.ElementType, out needsImport)}*"
            };
        }

        if (type is CppClass fieldType && fieldType.ClassKind == CppClassKind.Union) return "void*";
        // Forgive me for my sins
        var oldTypeName = type.GetDisplayName().Replace("const ", string.Empty);
        var ptrCount = oldTypeName.Count(x => x == '*');
        if (ptrCount == 0 && Config.ClassToGenerator.ContainsKey(oldTypeName))
            needsImport = true;

        string ptrs = new('*', ptrCount);
        oldTypeName = oldTypeName.Replace("*", string.Empty).Trim();
        if (STypeRenames.TryGetValue(oldTypeName, out var renamed))
            oldTypeName = renamed;
        return STypeConversions.TryGetValue(oldTypeName, out var converted)
            ? $"{converted}{ptrs}"
            : $"{oldTypeName}{ptrs}";
    }
}
