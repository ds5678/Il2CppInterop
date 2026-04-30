namespace Il2CppInterop.Common.Attributes;

/// <summary>
/// Indicates that a type should be source generated with Il2Cpp injection code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class InjectedTypeAttribute : Attribute
{
    /// <summary>
    /// The name of the type. If not specified, it will be inferred from the managed type's name.
    /// </summary>
    public string? Name { get; init; }
    /// <summary>
    /// The file name of the assembly that the type is defined in.
    /// If not specified, the assembly name will be inferred from the type's containing assembly.
    /// </summary>
    public string? Assembly { get; init; }
}
