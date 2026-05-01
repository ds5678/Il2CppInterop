using Il2CppInterop.StructGenerator.TypeGenerators;

namespace Il2CppInterop.StructGenerator;

internal static class Config
{
    /// <summary>
    /// NOTE: Ignores are handled BEFORE renames
    /// </summary>
    public static readonly string[] ClassForcedIgnores =
    [
        // Ignore the reflection structs
        "Il2CppPropertyInfo",
        "Il2CppMethodInfo"
    ];

    public static readonly Dictionary<string, string> ClassRenames = new()
    {
        ["TypeInfo"] = "Il2CppClass",
        ["FieldInfo"] = "Il2CppFieldInfo",
        ["EventInfo"] = "Il2CppEventInfo",
        ["ParameterInfo"] = "Il2CppParameterInfo",
        ["PropertyInfo"] = "Il2CppPropertyInfo",
        ["MethodInfo"] = "Il2CppMethodInfo"
    };

    public static readonly Dictionary<string, Type> ClassToGenerator = new()
    {
        ["Il2CppClass"] = typeof(Il2CppClassGenerator),
        ["Il2CppType"] = typeof(Il2CppTypeGenerator),
        ["Il2CppAssembly"] = typeof(Il2CppAssemblyGenerator),
        ["Il2CppAssemblyName"] = typeof(Il2CppAssemblyNameGenerator),
        ["Il2CppFieldInfo"] = typeof(Il2CppFieldInfoGenerator),
        ["Il2CppImage"] = typeof(Il2CppImageGenerator),
        ["Il2CppEventInfo"] = typeof(Il2CppEventInfoGenerator),
        ["Il2CppException"] = typeof(Il2CppExceptionGenerator),
        ["Il2CppParameterInfo"] = typeof(Il2CppParameterInfoGenerator),
        ["Il2CppPropertyInfo"] = typeof(Il2CppPropertyInfoGenerator),
        ["Il2CppMethodInfo"] = typeof(Il2CppMethodInfoGenerator)
    };
}
