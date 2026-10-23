# Value Types

## `readonly`

Structs that were originally readonly no longer are.

## `IValueType`

All value types implement this interface. It replaces `ValueType` anywhere that would normally be used, like in generic constraints.

`IValueType` has the instance members of `ValueType`, and `IEnum` those of `Enum`. `ValueType` and `Enum` override `Equals`, `GetHashCode` and `ToString` of `Object`, so their interfaces also override the corresponding members of `IObject`, and `IEnum` those of `IValueType`, with default implementations that call their own. A call through `IObject`, such as the `Equals`, `GetHashCode` and `ToString` overrides that value types get for .NET, therefore reaches the member of `ValueType` or `Enum`, unless the value type has its own. Il2Cpp does not choose the override itself: `il2cpp_runtime_invoke` calls the method it is given. Without these overrides, `Equals` compared two boxes by reference, and `ToString` of an enum value gave the name of its type.
