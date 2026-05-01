using System.Text;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal readonly record struct CodeGenEnumElement(string Name, string? Value = null)
{
    public string BuildFrom(CodeGenEnum origin)
    {
        StringBuilder builder = new($"{origin.IndentInner}{Name}");
        if (Value != null) builder.Append($" = {Value}");
        builder.Append(',');
        return builder.ToString();
    }
}
