using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.StructGenerator;

public static partial class Il2CppStructWrapperGenerator
{
    private static readonly Dictionary<int, List<VersionSpecificGenerator>> SGenerators = [];
    internal static ILogger? Logger { get; set; }

    private static VersionSpecificGenerator? VisitClass(CppClass @class, int metadataVersion, UnityVersion unityVersion)
    {
        if (Config.ClassForcedIgnores.Contains(@class.Name))
            return null;
        if (Config.ClassRenames.TryGetValue(@class.Name, out var rename))
            @class.Name = rename;
        if (!Config.ClassNames.Contains(@class.Name))
            return null;
        var existingVersionGeneratorCount =
            SGenerators[metadataVersion].Count(x => x.CppClassName == @class.Name);
        if (!Config.TryCreateGenerator(@class, $"{metadataVersion}_{existingVersionGeneratorCount}", out var generator))
            return null;

        foreach (var field in generator.NativeStructGenerator.FieldsToImport.ToList())
        {
            var cppField = generator.NativeStructGenerator.CppClass.Fields.Single(x => x.Name == field.Name);

            var typeClass = cppField.Type as CppClass ?? (cppField.Type as CppTypedef)?.ElementType as CppClass;

            if (typeClass != null)
            {
                var gen = VisitClass(typeClass, metadataVersion, unityVersion);
                if (gen == null)
                    continue;
                field.FieldType =
                    $"{gen.HandlerGenerator.HandlerClass.Name}.{gen.NativeStructGenerator.NativeStruct.Name}";
                generator.NativeStructGenerator.FieldsToImport.Remove(field);
                if (Config.ClassNames.Contains(gen.NativeStructGenerator.CppClass.Name))
                    generator.ExtraUsings.Add(
                        $"Il2CppInterop.Runtime.Runtime.VersionSpecific.{gen.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", null)}");
            }
        }
        generator.SetupElements();

        var existingGenerators = SGenerators.Values.SelectMany(x => x).Where(x => x.CppClassName == generator.CppClassName);
        foreach (var existingGenerator in existingGenerators)
        {
            if (existingGenerator.NativeStructGenerator.NativeStruct == generator.NativeStructGenerator.NativeStruct)
            {
                existingGenerator.ApplicableVersions.Add(unityVersion);
                return existingGenerator;
            }
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
            var metadataMatch = MetadataVersionRegex.Match(headerText);

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

            if (!SGenerators.ContainsKey(metadataVersion))
                SGenerators[metadataVersion] = [];
            var compilation = CppParser.Parse(ProcessHeaderText(headerText),
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
            foreach (var @class in compilation.Classes)
            {
                VisitClass(@class, metadataVersion, actualVersion);
            }
            previousVersion = version;
        }

        Logger?.LogInformation("Building version specific classes");
        Dictionary<Type, Dictionary<UnityVersion, VersionSpecificGenerator>> versionToGeneratorLookup = [];
        foreach (var generator in SGenerators.Values.SelectMany(x => x))
        {
            if (!versionToGeneratorLookup.TryGetValue(generator.GetType(), out var versionDictionary))
                versionDictionary = versionToGeneratorLookup[generator.GetType()] = [];

            foreach (var version in generator.ApplicableVersions)
                versionDictionary[version] = generator;
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
            var generatorOutputDirectory = Path.Join(options.OutputDirectory, generator.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", null));
            Directory.CreateDirectory(generatorOutputDirectory);
            CodeGenFile file = new()
            {
                Namespace = $"Il2CppInterop.Runtime.Runtime.VersionSpecific.{generator.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", null)}",
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
            file.Usings.AddRange(generator.ExtraUsings);
            file.WriteTo(Path.Join(generatorOutputDirectory, $"{generator.NativeStructGenerator.NativeStruct.Name.Replace("Il2Cpp", null)}.cs"));
        }

        Logger = null;
    }

    private static string ProcessHeaderText(string headerText)
    {
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

        return processedHeaderText;
    }

    [GeneratedRegex(@"const int METADATA_VERSION = ([0-9]+);")]
    private static partial Regex MetadataVersionRegex { get; }
}
