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
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToArray()!;
        }
    }
}
