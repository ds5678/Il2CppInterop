using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

// User code
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

// Source generated
[Il2CppType(typeof(Il2CppInternals))]
internal partial class Il2CppToMonoDelegateReference : IIl2CppType<Il2CppToMonoDelegateReference>
{
    public partial Il2CppSystem.IntPtr MethodInfo
    {
        get => FieldAccess.GetInstanceFieldValue<Il2CppSystem.IntPtr>(this, Il2CppInternals.FieldOffset_0);
        set => FieldAccess.SetInstanceFieldValue(this, Il2CppInternals.FieldOffset_0, value);
    }

    [Il2CppField(Name = nameof(ReferencedDelegate))]
    private Il2CppSystem.IntPtr ReferencedDelegate__BackingField
    {
        get => FieldAccess.GetInstanceFieldValue<Il2CppSystem.IntPtr>(this, Il2CppInternals.FieldOffset_1);
        set => FieldAccess.SetInstanceFieldValue(this, Il2CppInternals.FieldOffset_1, value);
    }
    public partial Delegate ReferencedDelegate
    {
        get => GCHandle<Delegate>.FromIntPtr(ReferencedDelegate__BackingField).Target;
        set
        {
            GCHandle<Delegate>.FromIntPtr(ReferencedDelegate__BackingField).Dispose();
            ReferencedDelegate__BackingField = value is not null ? GCHandle<Delegate>.ToIntPtr(new GCHandle<Delegate>(value)) : default;
        }
    }

    // If user doesn't write their own we need to generate one
    public Il2CppToMonoDelegateReference(ObjectPointer obj0) : base(obj0)
    {
    }

    [Il2CppMethod(Name = "Finalize")]
    public override void Il2CppFinalize()
    {
        // This disposal happens when the object is collected by the Il2Cpp GC instead of the managed GC.
        // That ensures that the delegate is kept alive as long as the Il2Cpp object is alive, even if the managed wrapper gets collected.
        // In theory, the managed wrapper could be collected and recreated multiple times during the lifetime of the Il2Cpp object,
        // so this ensures that the managed fields are not disposed prematurely.
        try
        {
            this.DisposeMethodInfo();
            ReferencedDelegate = null!;
        }
        catch (Exception ex)
        {
            LogErrorIl2CppFinalize(ex);
        }
        finally
        {
            base.Il2CppFinalize(); // Must call base method
        }
    }
    partial void LogErrorIl2CppFinalize(Exception exception);

    static int IIl2CppType<Il2CppToMonoDelegateReference>.Size => nint.Size;

    nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer;

    static Il2CppToMonoDelegateReference? IIl2CppType<Il2CppToMonoDelegateReference>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        return Il2CppType.ReadReference<Il2CppToMonoDelegateReference>(span);
    }

    static void IIl2CppType<Il2CppToMonoDelegateReference>.WriteToSpan(Il2CppToMonoDelegateReference? value, Span<byte> span)
    {
        Il2CppType.WriteReference(value, span);
    }

    static string IIl2CppType<Il2CppToMonoDelegateReference>.AssemblyName => "Assembly-CSharp.dll";

    static Il2CppToMonoDelegateReference()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Il2CppInternals).TypeHandle);
    }
}
file static class Il2CppInternals
{
    public static readonly int FieldOffset_0;
    public static readonly int FieldOffset_1;

    static Il2CppInternals()
    {
        TypeInjector.RegisterTypeInIl2Cpp<Il2CppToMonoDelegateReference>();
        FieldOffset_0 = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer, "MethodInfo"));
        FieldOffset_1 = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer, "ReferencedDelegate"));
        Il2CppObjectPool.RegisterInitializer(Il2CppClassPointerStore<Il2CppToMonoDelegateReference>.NativeClassPointer, ptr => new Il2CppToMonoDelegateReference(ptr));
    }
}
