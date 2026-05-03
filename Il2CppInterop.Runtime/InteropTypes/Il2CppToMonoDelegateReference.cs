using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.InteropTypes;

[InjectedType(Assembly = "Assembly-CSharp.dll")]
internal sealed partial class Il2CppToMonoDelegateReference : Object
{
    [Il2CppField]
    public partial Il2CppSystem.IntPtr MethodInfo { get; set; }
    [ManagedField]
    public partial Delegate ReferencedDelegate { get; set; }

    public Il2CppToMonoDelegateReference(Delegate referencedDelegate, IntPtr methodInfo) : this(IL2CPP.NewObjectPointer<Il2CppToMonoDelegateReference>())
    {
        ReferencedDelegate = referencedDelegate;
        MethodInfo = methodInfo;
    }

    [Il2CppFinalizer]
    private void DisposeMethodInfo()
    {
        Marshal.FreeHGlobal(MethodInfo);
        MethodInfo = IntPtr.Zero;
    }

    partial void LogErrorIl2CppFinalize(Exception exception)
    {
        Logger.Instance.LogError($"Exception in {nameof(Il2CppToMonoDelegateReference)}.{nameof(Il2CppFinalize)}: {{Exception}}", exception);
    }
}
