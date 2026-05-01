using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.Resources;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.StructGenerator;

public record Il2CppStructWrapperGeneratorOptions(
    string HeadersDirectory,
    string OutputDirectory,
    ILogger? Logger
);

public static class Il2CppStructWrapperGenerator
{
    private static readonly Dictionary<int, List<VersionSpecificGenerator>> SGenerators = new();
    internal static ILogger? Logger { get; set; }

    private static VersionSpecificGenerator? VisitClass(CppClass @class, int metadataVersion,
        UnityVersion unityVersion, CppClass[] classes)
    {
        if (Config.ClassForcedIgnores.Contains(@class.Name)) return null;
        if (Config.ClassRenames.TryGetValue(@class.Name, out var rename)) @class.Name = rename;
        if (!Config.ClassToGenerator.TryGetValue(@class.Name, out var generatorType)) return null;
        if (!typeof(VersionSpecificGenerator).IsAssignableFrom(generatorType))
            throw new Exception($"{@class.Name} has an invalid generator");

        var existingVersionGeneratorCount =
            SGenerators[metadataVersion].Count(x => x.GetType() == generatorType);
        var existingGenerators =
            SGenerators.Values.SelectMany(x => x).Where(x => x.GetType() == generatorType).ToList();
        var generator = (VersionSpecificGenerator)Activator.CreateInstance(generatorType,
            $"{metadataVersion}_{existingVersionGeneratorCount}", @class,
            new Func<string, CppClass>(dependencyName => { return classes.Single(x => x.Name == dependencyName); }))!;

        foreach (var field in generator.NativeStructGenerator.FieldsToImport.ToList())
        {
            var cppField = generator.NativeStructGenerator.CppClass.Fields.Single(x => x.Name == field.Name);

            CppClass? typeClass = null;
            if (cppField.Type is CppClass)
                typeClass = (CppClass)cppField.Type;
            if (cppField.Type is CppTypedef typeDef && typeDef.ElementType is CppClass)
                typeClass = (CppClass)typeDef.ElementType;
            if (typeClass != null)
            {
                var gen = VisitClass(typeClass, metadataVersion, unityVersion, classes);
                if (gen == null) continue;
                field.FieldType =
                    $"{gen.HandlerGenerator.HandlerClass.Name}.{gen.NativeStructGenerator.NativeStruct.Name}";
                generator.NativeStructGenerator.FieldsToImport.Remove(field);
                if (Config.ClassToGenerator.ContainsKey(gen.NativeStructGenerator.CppClass.Name))
                    generator.AddExtraUsing(
                        $"Il2CppInterop.Runtime.Runtime.VersionSpecific.{gen.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", string.Empty)}");
            }
        }

        generator.SetupElements();
        foreach (var existingGenerator in existingGenerators)
            if (existingGenerator.NativeStructGenerator.NativeStruct == generator.NativeStructGenerator.NativeStruct)
            {
                existingGenerator.ApplicableVersions.Add(unityVersion);
                return existingGenerator;
            }

        generator.ApplicableVersions.Add(unityVersion);
        SGenerators[metadataVersion].Add(generator);
        return generator;
    }

    public static void Generate(Il2CppStructWrapperGeneratorOptions options)
    {
        Logger = options.Logger;
        if (Directory.Exists(options.OutputDirectory))
            Directory.Delete(options.OutputDirectory, true);
        Directory.CreateDirectory(options.OutputDirectory);
        var previousVersion = UnityVersion.MinVersion;
        foreach (var (headerPath, version) in Directory.GetFiles(options.HeadersDirectory, "*.h")
                     .Select(x => (x, UnityVersion.Parse(Path.GetFileNameWithoutExtension(x)))).OrderBy(x => x.Item2))
        {
            var headerText = File.ReadAllText(headerPath);
            var metadataMatch = Regex.Match(headerText, @"const int METADATA_VERSION = ([0-9]+);");

            int metadataVersion;
            if (metadataMatch.Success)
            {
                metadataVersion = int.Parse(metadataMatch.Groups[1].Value);
            }
            else
            {
                Logger?.LogWarning("{} has an invalid metadata version", version);
                continue;
            }

            const string HeaderPrefix = """
                #line 1 "Prefix.h"
                typedef int int32_t;
                typedef unsigned int uint32_t;
                typedef short int16_t;
                typedef unsigned short uint16_t;
                typedef char int8_t;
                typedef unsigned char uint8_t;
                typedef long long int64_t;
                typedef unsigned long long uint64_t;
                typedef long intptr_t;
                typedef unsigned long uintptr_t;
                """;

            string processedHeaderText;
            if (headerText.Contains("struct ParameterInfo", StringComparison.Ordinal))
            {
                processedHeaderText = $"""
                    {HeaderPrefix}
                    #line 1 "Header.h"
                    {headerText}
                    """;
            }
            else
            {
                // ParameterInfo was removed in v27, but we add it back in manually.
                processedHeaderText = $$"""
                    {{HeaderPrefix}}
                    #line 1 "ParameterInfo.h"
                    typedef struct Il2CppType Il2CppType;
                    typedef struct ParameterInfo
                    {
                        const Il2CppType* parameter_type;
                    } ParameterInfo;
                    #line 1 "Header.h"
                    {{headerText.Replace("const Il2CppType** parameters;", "const ParameterInfo* parameters;")}}
                    """;
            }

            if (!SGenerators.ContainsKey(metadataVersion))
                SGenerators[metadataVersion] = new List<VersionSpecificGenerator>();
            var compilation = CppParser.Parse(processedHeaderText,
                new CppParserOptions
                {
                    ParserKind = CppParserKind.Cpp,
                    AutoSquashTypedef = false,
                    ParseMacros = false
                });
            Logger?.LogInformation("Parsing {}", version);
            if (compilation.HasErrors)
            {
                Logger?.LogError("Failed to parse {}", version);
                continue;
            }
            // If this is the first version with this major.minor.build, strip the rest of the information.
            var actualVersion = version.Major == previousVersion.Major && version.Minor == previousVersion.Minor && version.Build == previousVersion.Build
                ? version
                : new UnityVersion(version.Major, version.Minor, version.Build);
            var classes = compilation.Classes.ToArray();
            foreach (var @class in classes)
            {
                VisitClass(@class, metadataVersion, actualVersion, classes);
            }
            previousVersion = version;
        }

        Logger?.LogInformation("Building version specific classes");
        Dictionary<Type, Dictionary<UnityVersion, VersionSpecificGenerator>> versionToGeneratorLookup = new();
        foreach (var generator in SGenerators.Values.SelectMany(x => x))
        {
            if (!versionToGeneratorLookup.ContainsKey(generator.GetType()))
                versionToGeneratorLookup[generator.GetType()] =
                    new Dictionary<UnityVersion, VersionSpecificGenerator>();

            foreach (var version in generator.ApplicableVersions)
                versionToGeneratorLookup[generator.GetType()][version] = generator;
        }

        foreach (var kvp in versionToGeneratorLookup)
        {
            VersionSpecificGenerator? last = null;
            foreach (var kvp2 in kvp.Value.Where(kvp2 => last is null || last != kvp2.Value))
            {
                var versionString = kvp2.Key is { Type: UnityVersionType.Alpha, TypeNumber: 0 }
                    ? kvp2.Key.ToStringWithoutType()
                    : kvp2.Key.ToString();
                kvp2.Value.HandlerGenerator.HandlerClass.Attributes.Add(
                    $"ApplicableToUnityVersionsSince(\"{versionString}\")");
                last = kvp2.Value;
            }
        }

        foreach (var generator in SGenerators.Values.SelectMany(x => x))
        {
            var generatorOutputDir =
                Path.Combine(options.OutputDirectory,
                    generator.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", string.Empty));
            if (!Directory.Exists(generatorOutputDir))
                Directory.CreateDirectory(generatorOutputDir);
            CodeGenFile file = new()
            {
                Namespace =
                    $"Il2CppInterop.Runtime.Runtime.VersionSpecific.{generator.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", string.Empty)}",
                Usings =
                {
                    "System",
                    "System.Runtime.InteropServices"
                },
                Elements =
                {
                    generator.HandlerGenerator.HandlerClass
                }
            };
            foreach (var extraUsing in generator.ExtraUsings)
                file.Usings.Add(extraUsing);
            file.WriteTo(Path.Combine(generatorOutputDir,
                $"{generator.NativeStructGenerator.NativeStruct.Name.Replace("Il2Cpp", string.Empty)}.cs"));
        }

        Logger = null;
    }
}
