namespace AdoToolkit.Core.TestManagement;

internal sealed class StepExpander(IReadOnlyDictionary<int, ResolvedStepDocument> cache, Uri collection,
    int maximumDepth, int maximumRows, CultureInfo culture, CancellationToken cancellationToken)
{
    internal ExpansionContext Expand(StepDocument root)
    {
        ExpansionContext context = new();
        Visit(root, [root.WorkItemId], string.Empty, context);
        return context;
    }

    private void Visit(StepDocument document, List<int> activePath, string prefix, ExpansionContext context)
    {
        if (context.Stopped) return;
        if (context.DiagnosedDocuments.Add(document.WorkItemId))
            context.Diagnostics.AddRange(document.Diagnostics.Where(d => d.Code != DiagnosticCodes.InvalidSharedStepReference));
        int position = 0;
        foreach (StepNode node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Stopped) return;
            string number = prefix + (++position).ToString(CultureInfo.InvariantCulture);
            IReadOnlyList<AdoSharedStepInfo> path = Array.AsReadOnly(activePath.Skip(1).Select(id => Info(id)).ToArray());
            string? code = null;
            ResolvedStepDocument? referenced = null;
            bool group = node.Kind == AdoTestStepKind.SharedStep;
            if (group)
            {
                if (node.SharedStepId is not int id) code = DiagnosticCodes.InvalidSharedStepReference;
                else if (activePath.Contains(id)) code = DiagnosticCodes.CircularSharedStepReference;
                else if (activePath.Count - 1 == maximumDepth) code = DiagnosticCodes.MaximumDepthExceeded;
                else if (cache.TryGetValue(id, out referenced) && referenced.ResolutionLimited) code = DiagnosticCodes.ResolutionLimitExceeded;
                else if (referenced?.Item is null) code = DiagnosticCodes.UnresolvedSharedStep;
                else if (!referenced.HasStepsData) code = DiagnosticCodes.SharedStepHasNoSteps;
            }
            context.Rows.Add(new AdoTestStep
            {
                Sequence = ++context.Sequence, Number = number, Kind = node.Kind, Action = node.Action, ExpectedResult = node.ExpectedResult,
                ActionSource = node.ActionSource, ExpectedResultSource = node.ExpectedResultSource,
                SourceWorkItemId = document.WorkItemId, SourceRev = document.Rev, SourceStepId = node.SourceStepId,
                SharedStepPath = path, SharedStep = group && node.SharedStepId is int referenceId ? Info(referenceId) : null,
                IsExpanded = group && code is null, DiagnosticCode = code,
            });
            if (code is not null)
                context.Diagnostics.Add(DiagnosticMessageRenderer.Create(code, culture, document.WorkItemId, stepNumber: number,
                    referenceChain: code is DiagnosticCodes.CircularSharedStepReference or DiagnosticCodes.MaximumDepthExceeded
                        ? activePath.Append(node.SharedStepId!.Value) : null));
            else if (group)
                Visit(referenced!.Document!, [.. activePath, node.SharedStepId!.Value], number + ".", context);

            // §10.5.2: the check follows the node (and recursive expansion); retain the row that crossed the limit.
            if (!context.Stopped && context.Rows.Count > maximumRows)
            {
                context.Rows.Add(new AdoTestStep { Sequence = ++context.Sequence, Number = number,
                    Kind = AdoTestStepKind.Truncated, SourceWorkItemId = document.WorkItemId, SourceRev = document.Rev,
                    SharedStepPath = path, DiagnosticCode = DiagnosticCodes.ExpansionLimitExceeded });
                context.Diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.ExpansionLimitExceeded, culture,
                    document.WorkItemId, stepNumber: number));
                context.Stopped = true;
            }
        }
    }

    internal AdoSharedStepInfo Info(int id, int count = 0) => cache.TryGetValue(id, out ResolvedStepDocument? resolved)
        && resolved.Item is not null ? resolved.Item.Info(collection, count) : new() { Id = id, ReferenceCount = count };
}
