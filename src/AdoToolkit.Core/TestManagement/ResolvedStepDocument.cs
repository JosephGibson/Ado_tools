namespace AdoToolkit.Core.TestManagement;

internal sealed class ResolvedStepDocument
{
    internal TestWorkItem? Item { get; init; }
    internal StepDocument? Document { get; init; }
    internal bool ResolutionLimited { get; init; }
    internal bool HasStepsData => !string.IsNullOrWhiteSpace(Item?.Text("Microsoft.VSTS.TCM.Steps"));
}
