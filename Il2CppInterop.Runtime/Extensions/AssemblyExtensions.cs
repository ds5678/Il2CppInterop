using System;
using System.Linq;
using System.Reflection;

namespace Il2CppInterop.Runtime.Extensions;

internal static class AssemblyExtensions
{
    public static Type[] GetTypesSafe(this Assembly assembly)
    {
        try
        {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            return assembly.GetTypes();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToArray()!;
        }
    }
}
