namespace Il2CppInterop.StructGenerator;

internal static class Program
{
    internal static void Main(string[] args)
    {
        // Directory that contains libil2cpp headers. Directory must contains subdirectories named after libil2cpp version.
        var headersDirectory = args[0];

        // Directory to write managed struct wrapper sources to
        var outputDirectory = args[1];

        Il2CppStructWrapperGeneratorOptions options = new(headersDirectory, outputDirectory, null);
        Il2CppStructWrapperGenerator.Generate(options);
    }
}
