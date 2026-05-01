namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenStruct : CodeGenClass
{
    public CodeGenStruct(ElementProtection protection, string name) : base(protection, name)
    {
    }

    public override string Type => "struct";
}
