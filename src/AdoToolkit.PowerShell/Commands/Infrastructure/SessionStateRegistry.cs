using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;

namespace AdoToolkit.Commands.Infrastructure;

internal static class SessionStateRegistry
{
    private static readonly ConditionalWeakTable<Runspace, SessionStateHolder> Holders = new();

    internal static SessionStateHolder Current => Holders.GetValue(Runspace.DefaultRunspace
        ?? throw new InvalidOperationException(nameof(Runspace.DefaultRunspace)), static runspace =>
    {
        SessionStateHolder holder = new();
        runspace.StateChanged += (_, args) =>
        {
            if (args.RunspaceStateInfo.State is RunspaceState.Closed or RunspaceState.Broken) holder.Disconnect();
        };
        return holder;
    });
}
