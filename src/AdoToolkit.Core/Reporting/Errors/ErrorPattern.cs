namespace AdoToolkit.Core.Reporting.Errors;

// A wildcard pattern of an error rule: * stands for any text and ? for one character, and the
// pattern must match a whole line. Pattern and line are both folded (FoldedText), so case, accents,
// apostrophes and runs of spaces do not count. The segments between stars are found from left to
// right, a segment with ? by a shift-and search, so a match is linear in the line: no regular
// expression, no backtracking and no timeout.
internal sealed class ErrorPattern
{
    private readonly Segment[] segments;
    private readonly bool leadingStar;
    private readonly bool trailingStar;
    // The longest run of plain characters, which every matching line contains.
    private readonly string required;

    internal ErrorPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Text = pattern;
        string folded = FoldedText.Of(pattern).Text;
        leadingStar = folded.StartsWith('*');
        trailingStar = folded.EndsWith('*');
        segments = [.. folded.Split('*', StringSplitOptions.RemoveEmptyEntries).Select(static text => new Segment(text))];
        required = segments.SelectMany(static segment => segment.Text.Split('?')).OrderByDescending(static run => run.Length).FirstOrDefault() ?? string.Empty;
    }

    // The pattern as written.
    internal string Text { get; }

    // Whether the pattern matches the whole of a folded line.
    internal bool IsMatch(string folded)
    {
        ArgumentNullException.ThrowIfNull(folded);
        if (required.Length > 0 && !folded.Contains(required, StringComparison.Ordinal)) return false;
        if (segments.Length == 0) return leadingStar || folded.Length == 0;
        int position = 0, first = 0, last = segments.Length - 1, limit = folded.Length;
        if (!leadingStar)
        {
            if (!segments[0].MatchesAt(folded, 0)) return false;
            position = segments[0].Length;
            first = 1;
            if (segments.Length == 1 && !trailingStar) return position == folded.Length;
        }
        if (!trailingStar && first <= last)
        {
            int at = folded.Length - segments[last].Length;
            if (at < position || !segments[last].MatchesAt(folded, at)) return false;
            limit = at;
            last--;
        }
        for (int index = first; index <= last; index++)
        {
            int found = segments[index].IndexOf(folded, position, limit);
            if (found < 0) return false;
            position = found + segments[index].Length;
        }
        return true;
    }

    // Text between two stars. With no ?, a vectorized search finds it; with ?, a shift-and search
    // keeps one bit per pattern position and reads each character of the line once.
    private sealed class Segment
    {
        private readonly bool wildcards;
        private readonly Dictionary<char, ulong[]> masks = [];
        private readonly ulong[] anyCharacter;

        internal Segment(string text)
        {
            Text = text;
            wildcards = text.Contains('?', StringComparison.Ordinal);
            int words = (text.Length + 63) / 64;
            anyCharacter = new ulong[words];
            if (!wildcards) return;
            for (int index = 0; index < text.Length; index++)
                if (text[index] == '?') anyCharacter[index / 64] |= 1UL << (index % 64);
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '?') continue;
                if (!masks.TryGetValue(text[index], out ulong[]? mask)) masks[text[index]] = mask = [.. anyCharacter];
                mask[index / 64] |= 1UL << (index % 64);
            }
        }

        internal string Text { get; }
        internal int Length => Text.Length;

        internal bool MatchesAt(string line, int at)
        {
            if (at < 0 || at + Text.Length > line.Length) return false;
            for (int index = 0; index < Text.Length; index++)
                if (Text[index] != '?' && Text[index] != line[at + index]) return false;
            return true;
        }

        // The first position at or after from where the segment fits before limit, or -1.
        internal int IndexOf(string line, int from, int limit)
        {
            if (limit - from < Text.Length) return -1;
            if (!wildcards) return line.IndexOf(Text, from, limit - from, StringComparison.Ordinal);
            int words = anyCharacter.Length, top = (Text.Length - 1) / 64;
            ulong last = 1UL << ((Text.Length - 1) % 64);
            Span<ulong> state = words <= 16 ? stackalloc ulong[words] : new ulong[words];
            state.Clear();
            for (int index = from; index < limit; index++)
            {
                ulong[] mask = masks.TryGetValue(line[index], out ulong[]? known) ? known : anyCharacter;
                ulong carry = 1;
                for (int word = 0; word < words; word++)
                {
                    ulong shifted = (state[word] << 1) | carry;
                    carry = state[word] >> 63;
                    state[word] = shifted & mask[word];
                }
                if ((state[top] & last) != 0) return index - Text.Length + 1;
            }
            return -1;
        }
    }
}
