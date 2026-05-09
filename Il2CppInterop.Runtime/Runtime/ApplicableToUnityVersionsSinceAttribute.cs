using System;

namespace Il2CppInterop.Runtime.Runtime;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class ApplicableToUnityVersionsSinceAttribute : Attribute
{
    public ApplicableToUnityVersionsSinceAttribute(string startVersion)
    {
        StartVersion = startVersion;
    }

    public string StartVersion { get; }
}
