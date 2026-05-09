using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public sealed class SystemInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "System Interface Implementations";
    public override string Id => "system_interface_implementations";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // Should handle INotifyCompletion, IEnumerable, IEquatable, etc
    }
}
