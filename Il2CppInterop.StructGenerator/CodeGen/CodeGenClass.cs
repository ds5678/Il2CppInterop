using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenClass : CodeGenElement
{
    public CodeGenClass(ElementProtection protection, string name) : base(protection, name)
    {
    }

    public override string Type => "class";
    public List<string> InterfaceNames { get; } = new();
    public List<string> Attributes { get; } = new();
    public List<CodeGenMethod> Methods { get; } = new();
    public List<CodeGenField> Fields { get; } = new();
    public List<CodeGenProperty> Properties { get; } = new();
    public List<CodeGenElement> NestedElements { get; } = new();

    public override void Build(IndentedTextWriter writer)
    {
        foreach (var attribute in Attributes)
        {
            writer.WriteLine($"[{attribute}]");
        }
        base.Build(writer);
        if (InterfaceNames.Count > 0)
        {
            writer.Write(" : ");
            writer.Write(InterfaceNames[0]);
            for (var i = 1; i < InterfaceNames.Count; i++)
            {
                writer.Write(", ");
                writer.Write(InterfaceNames[i]);
            }
        }
        writer.WriteLine();
        using (new CurlyBrackets(writer))
        {
            foreach (var method in Methods)
                method.Build(writer);
            foreach (var field in Fields)
                field.Build(writer);
            foreach (var property in Properties)
                property.Build(writer);
            foreach (var nestedElement in NestedElements)
                nestedElement.Build(writer);
        }
    }

    public static bool operator !=(CodeGenClass lhs, CodeGenClass rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenClass lhs, CodeGenClass rhs)
    {
        if (lhs.Fields.Count != rhs.Fields.Count) return false;
        if (lhs.NestedElements.Count != rhs.NestedElements.Count) return false;
        for (var i = 0; i < lhs.Fields.Count; i++)
            if (lhs.Fields[i] != rhs.Fields[i])
                return false;
        for (var i = 0; i < lhs.NestedElements.Count; i++)
        {
            if (lhs.NestedElements[i] is not CodeGenEnum lhsEnum) continue;
            if (rhs.NestedElements[i] is not CodeGenEnum rhsEnum) continue;
            if (lhsEnum != rhsEnum) return false;
        }

        return true;
    }

    public override bool Equals(object obj)
    {
        return obj is CodeGenClass @class && this == @class;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Fields.Count, NestedElements.Count);
    }
}
