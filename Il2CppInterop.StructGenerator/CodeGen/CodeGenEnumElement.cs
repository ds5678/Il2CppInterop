using System.Text;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenEnumElement
{
    public CodeGenEnumElement(string name, string? value = null)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string? Value { get; }

    public string BuildFrom(CodeGenEnum origin)
    {
        StringBuilder builder = new($"{origin.IndentInner}{Name}");
        if (Value != null) builder.Append($" = {Value}");
        builder.Append(',');
        return builder.ToString();
    }

    public static bool operator !=(CodeGenEnumElement lhs, CodeGenEnumElement rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenEnumElement lhs, CodeGenEnumElement rhs)
    {
        if (lhs.Name != rhs.Name) return false;
        return lhs.Value == rhs.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is CodeGenEnumElement element && this == element;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Value);
    }
}
