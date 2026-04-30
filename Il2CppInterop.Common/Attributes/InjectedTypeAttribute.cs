namespace Il2CppInterop.Common.Attributes;

/// <summary>
/// Indicates that a type should be source generated with Il2Cpp injection code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class InjectedTypeAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Assembly { get; init; }
}
