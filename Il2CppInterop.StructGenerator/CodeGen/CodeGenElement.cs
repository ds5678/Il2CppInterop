namespace Il2CppInterop.StructGenerator.CodeGen;

internal abstract class CodeGenElement
{
    public CodeGenElement(ElementProtection protection, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Protection = protection;
        Name = name;
    }

    public abstract byte IndentAmount { get; set; }
    public abstract string Type { get; }

    public bool IsStatic { get; set; }
    public bool IsUnsafe { get; set; }
    public string Name { get; }
    public ElementProtection Protection { get; }
    public string Indent => new(' ', (IndentAmount - 1) * 4);
    public string IndentInner => new(' ', IndentAmount * 4);

    public string Keywords
    {
        get
        {
            if (IsStatic)
            {
                return IsUnsafe ? "static unsafe " : "static ";
            }
            else
            {
                return IsUnsafe ? "unsafe " : string.Empty;
            }
        }
    }

    public virtual string Declaration => $"{Protection.ToCSharpString()} {Keywords}{Type} {Name}";

    public virtual string Build()
    {
        return $"{Declaration}";
    }
}
