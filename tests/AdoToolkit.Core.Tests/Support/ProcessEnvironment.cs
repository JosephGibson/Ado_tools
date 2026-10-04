namespace AdoToolkit.Core.Tests.Support;

// Classes that change process-wide state, such as an environment variable the product reads, join
// this collection. xUnit runs it after the parallel collections, with nothing beside it.
// ProcessStateTests keeps every such class here.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironment
{
    public const string Name = "Process environment";
}
