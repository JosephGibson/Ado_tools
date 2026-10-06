namespace AdoToolkit.Core.Reporting.Errors;

// The lines of one error message as the classifier reads them (ErrorText.ScannedLines), each folded
// once, when a template first needs it.
internal sealed class ErrorLines
{
    private readonly FoldedText?[] folded;

    internal ErrorLines(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Lines = lines;
        folded = new FoldedText?[lines.Count];
        KeyIndex = lines.Count == 0 ? -1 : ErrorText.KeyLineIndex(lines);
    }

    internal IReadOnlyList<string> Lines { get; }
    internal int Count => Lines.Count;
    // The key line's index, or -1 for no line.
    internal int KeyIndex { get; }

    internal FoldedText Folded(int index) => folded[index] ??= FoldedText.Of(Lines[index]);
}
