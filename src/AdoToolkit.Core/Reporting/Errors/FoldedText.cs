using System.Text;

namespace AdoToolkit.Core.Reporting.Errors;

// One line folded for comparison: decomposed, without nonspacing marks, in invariant lower case,
// with typographic apostrophes as ' and every run of white space, no-break spaces included, as one
// space, trimmed. The folded text only ever serves matching: each folded character maps back to the
// characters it came from, so a part found in it is shown exactly as the server wrote it.
internal sealed class FoldedText
{
    private readonly int[] starts;
    private readonly int[] ends;

    private FoldedText(string original, string text, int[] starts, int[] ends)
    {
        Original = original;
        Text = text;
        this.starts = starts;
        this.ends = ends;
    }

    internal string Original { get; }
    internal string Text { get; }

    internal static FoldedText Of(string original)
    {
        ArgumentNullException.ThrowIfNull(original);
        StringBuilder text = new(original.Length);
        List<int> starts = new(original.Length), ends = new(original.Length);
        Span<char> buffer = stackalloc char[2];
        int index = 0;
        while (index < original.Length)
        {
            char c = original[index];
            bool pair = char.IsHighSurrogate(c) && index + 1 < original.Length && char.IsLowSurrogate(original[index + 1]);
            int end = index + (pair ? 2 : 1);
            if (!pair && char.IsWhiteSpace(c))
            {
                // A run of white space is one space, and none leads.
                if (text.Length > 0 && text[^1] == ' ') ends[^1] = end;
                else if (text.Length > 0) Add(' ', index, end);
            }
            else if (!pair && c < 0x80) Add(char.ToLowerInvariant(c), index, end);
            else if (c is '’' or '‘' or 'ʼ') Add('\'', index, end);
            // A lone surrogate half, or a noncharacter such as U+FFFE, cannot be normalized; it is
            // compared as it came.
            else if (!pair && char.IsSurrogate(c)) Add(c, index, end);
            else if (Decomposed(original[index..end]) is not { } decomposed) { for (int k = index; k < end; k++) Add(original[k], index, end); }
            else
            {
                foreach (Rune rune in decomposed.EnumerateRunes())
                {
                    if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark) continue;
                    int written = Rune.ToLowerInvariant(rune).EncodeToUtf16(buffer);
                    for (int k = 0; k < written; k++) Add(buffer[k], index, end);
                }
            }
            index = end;
        }
        if (text.Length > 0 && text[^1] == ' ')
        {
            text.Length--;
            starts.RemoveAt(starts.Count - 1);
            ends.RemoveAt(ends.Count - 1);
        }
        return new FoldedText(original, text.ToString(), [.. starts], [.. ends]);

        void Add(char folded, int start, int stop)
        {
            text.Append(folded);
            starts.Add(start);
            ends.Add(stop);
        }
    }

    // A character in normalization form D, or null for one that normalization refuses.
    private static string? Decomposed(string character)
    {
        try { return character.Normalize(NormalizationForm.FormD); }
        catch (ArgumentException) { return null; }
    }

    // The text as sent that the folded characters [start, start + length) came from.
    internal string Slice(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, Text.Length);
        return length == 0 ? string.Empty : Original[starts[start]..ends[start + length - 1]];
    }

    // The position in the text as sent where folded character index starts, or its length at the end.
    internal int OriginalStart(int index) => index < Text.Length ? starts[index] : Original.Length;

    // The position in the text as sent just after folded character index - 1.
    internal int OriginalEnd(int index) => index == 0 ? (Text.Length == 0 ? 0 : starts[0]) : ends[index - 1];
}
