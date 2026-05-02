using Microsoft.CodeAnalysis.Testing;

namespace Il2CppInterop.SourceGenerator.Tests;

internal static class DiagnosticResultExtensions
{
    public static DiagnosticResult WithSpan(this DiagnosticResult result, LinePositionSpan span)
    {
        return result.WithSpan(span.StartLine, span.StartColumn, span.EndLine, span.EndColumn);
    }
}
