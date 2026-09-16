namespace AdoToolkit.Commands.Infrastructure;

internal enum PipelineEventKind { Verbose, Debug, Warning, Progress }
internal sealed record PipelineEvent(PipelineEventKind Kind, string Message, AdoProgress? Progress = null);
