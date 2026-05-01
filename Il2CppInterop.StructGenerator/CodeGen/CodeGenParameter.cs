using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenParameter : CodeGenElement
{
    public CodeGenParameter(string parameterType, string name) : base(ElementProtection.Private, name)
    {
        Type = parameterType;
    }

    public override string Type { get; }

    public override void Build(IndentedTextWriter writer)
    {
        writer.Write(Type);
        writer.Write(' ');
        writer.Write(Name);
    }
}
