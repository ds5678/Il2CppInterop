using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Il2CppInterop.SourceGenerator.Tests;

public class SimpleAnalyzerTests : AnalyzerTests
{
    [Test]
    public Task TypeNotPartial()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public class Sample : Il2CppSystem.Object
            {
            }
            """;

        var expectedDiagnostic = new DiagnosticResult(InjectedTypeAnalyzer.MustBePartial)
            .WithSpan(LinePositionSpan.FindInSource(testCode, "Sample"))
            .WithArguments("Sample");

        return TestDiagnostic(testCode, expectedDiagnostic);
    }
}
