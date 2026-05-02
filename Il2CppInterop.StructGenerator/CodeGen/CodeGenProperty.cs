using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenProperty : CodeGenElement
{
    public CodeGenProperty(string propertyType, ElementProtection? protection, string name) : base(protection, name)
    {
        Type = propertyType;
    }

    public override string Type { get; }

    public string? ImmediateGet { get; set; }

    public bool EmptyGet { get; set; }
    public CodeGenMethod? GetMethod { get; set; }

    public bool EmptySet { get; set; }
    public CodeGenMethod? SetMethod { get; set; }

    public bool HasGet => GetMethod != null || ImmediateGet != null || EmptyGet;
    public bool HasSet => SetMethod != null || EmptySet;

    public override void Build(IndentedTextWriter writer)
    {
        base.Build(writer);
        if (ImmediateGet != null)
        {
            writer.WriteLine($" => {ImmediateGet};");
        }
        else if (SetMethod == null && GetMethod != null && GetMethod.ImmediateReturn != null)
        {
            GetMethod.BuildBody(writer);
        }
        else if (EmptyGet || EmptySet)
        {
            writer.Write(" {");
            if (EmptyGet) writer.Write(" get;");
            if (EmptySet) writer.Write(" set;");
            writer.WriteLine(" }");
        }
        else
        {
            writer.WriteLine();
            using (new CurlyBrackets(writer))
            {
                if (GetMethod != null)
                {
                    writer.Write("get");
                    GetMethod.BuildBody(writer);
                }
                if (SetMethod != null)
                {
                    writer.Write("set");
                    SetMethod.BuildBody(writer);
                }
            }
        }
    }
}
