using System.Diagnostics;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.Utilities;

namespace Il2CppInterop.StructGenerator;

internal class NativeStructGenerator
{
    public NativeStructGenerator(string metadataSuffix, CppClass cppClass)
    {
        CppClass = cppClass;
        NativeStruct = new CodeGenStruct(ElementProtection.Internal, $"{cppClass.Name}_{metadataSuffix}")
        {
            IsUnsafe = true
        };
        FillStruct();
    }

    public List<CodeGenField> FieldsToImport { get; } = new();
    public CodeGenStruct NativeStruct { get; }
    public CppClass CppClass { get; }

    private void FillStruct()
    {
        List<CodeGenEnum> bitfields = new();
        CodeGenEnum? lastBitfield = null;
        var bitfieldNextBit = 0;

        bool VisitBitfieldElement(CppField field)
        {
            if (field.BitFieldWidth == 16)
            {
                NativeStruct.Fields.Add(new CodeGenField("ushort", ElementProtection.Public, field.Name));
                return true;
            }

            if (field.BitFieldWidth == 8)
            {
                NativeStruct.Fields.Add(new CodeGenField("byte", ElementProtection.Public, field.Name));
                return true;
            }

            if (lastBitfield is null)
                lastBitfield = new CodeGenEnum(EnumUnderlyingType.Byte, ElementProtection.Internal,
                    $"Bitfield{bitfields.Count}");
            if (field.BitFieldWidth != 1)
            {
                bitfieldNextBit += field.BitFieldWidth;
                return false;
            }

            var bitIdx = bitfieldNextBit++;
            lastBitfield.Elements.Add(new CodeGenEnumElement($"BIT_{field.Name}", $"{bitIdx}"));
            lastBitfield.Elements.Add(
                new CodeGenEnumElement(field.Name, $"({field.BitFieldWidth} << BIT_{field.Name})"));
            if (lastBitfield.UnderlyingTypeSize * 8 < bitfieldNextBit)
                lastBitfield.UnderlyingType += 1;
            return false;
        }

        void FinalizeBitfield()
        {
            if (lastBitfield is null) return;
            NativeStruct.Fields.Add(new CodeGenField(lastBitfield.Name, ElementProtection.Public,
                $"_{lastBitfield.Name.ToLower()}"));
            bitfields.Add(lastBitfield);
            lastBitfield = null;
            bitfieldNextBit = 0;
        }

        Debug.Assert(CppClass.BaseTypes.Count == 0);

        foreach (var field in CppClass.Fields)
        {
            var normalizedType = ConversionUtils.CppTypeToCSharpName(field.Type, out var needsImport);
            if (string.IsNullOrEmpty(normalizedType)) continue;

            if (field.IsBitField)
            {
                if (bitfieldNextBit == 8) FinalizeBitfield();
                if (VisitBitfieldElement(field)) FinalizeBitfield();
            }
            else
            {
                FinalizeBitfield();
                CodeGenField codeGenField = new(normalizedType, ElementProtection.Public,
                    ConversionUtils.GetName(field));
                if (needsImport) FieldsToImport.Add(codeGenField);
                NativeStruct.Fields.Add(codeGenField);
            }
        }

        FinalizeBitfield();
        NativeStruct.NestedElements.AddRange(bitfields);
    }

    public string Build()
    {
        return NativeStruct.Build();
    }
}
