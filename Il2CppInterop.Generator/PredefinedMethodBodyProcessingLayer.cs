using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class PredefinedMethodBodyProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Predefined Method Body Processor";
    public override string Id => "predefined_method_body_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {

    }
}
