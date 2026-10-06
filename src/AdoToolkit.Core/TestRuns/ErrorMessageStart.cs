namespace AdoToolkit.Core.TestRuns;

// The start of a listed result's error message that history keeps (D-8): its first lines, as many
// as the classifier of the report reads, as the listing sent them, never the rest. A line too long
// for the memory of a large history is cut too, never inside a surrogate pair.
internal static class ErrorMessageStart
{
    internal const int MaximumLines = 20;
    internal const int MaximumCharacters = 8192;
    // Azure DevOps Services cuts a listed message at 4,000 characters (V-34); a start of that length
    // or more may have lost what tells two errors apart.
    internal const int ListingCut = 4000;

    internal static string? Of(string? message)
    {
        if (string.IsNullOrEmpty(message)) return null;
        int position = 0;
        for (int line = 0; line < MaximumLines && position < message.Length && position <= MaximumCharacters; line++)
        {
            int end = message.IndexOfAny(['\r', '\n'], position);
            if (end < 0) { position = message.Length; break; }
            position = end + (message[end] == '\r' && end + 1 < message.Length && message[end + 1] == '\n' ? 2 : 1);
        }
        int length = Math.Min(position, MaximumCharacters);
        if (length < message.Length && length > 0 && char.IsHighSurrogate(message[length - 1])) length--;
        // The line break that ends the last line kept belongs to the line after it.
        string start = message[..length];
        return length < message.Length ? start.TrimEnd('\r', '\n') : start;
    }
}
