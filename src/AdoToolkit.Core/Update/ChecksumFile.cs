using System.Text;

namespace AdoToolkit.Core.Update;

// The published <zip>.sha256 file, read as Install-AdoToolkit.ps1 reads it: the first non-blank line
// holds a 64-digit hexadecimal hash, then optionally the zip's name, with or without a leading '*'.
// The name compares ordinally where the installer's -ne ignores case: stricter, never weaker.
internal static class ChecksumFile
{
    // The characters of .NET's \s, as the installer's regular expression reads them.
    private static readonly System.Buffers.SearchValues<char> Whitespace = System.Buffers.SearchValues.Create(
        Enumerable.Range(0, char.MaxValue + 1).Select(static code => (char)code).Where(char.IsWhiteSpace).ToArray());

    internal static string ReadHash(ReadOnlySpan<byte> content, string fileName, string archiveName, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        string text;
        try
        {
            ReadOnlySpan<byte> body = content.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? content[3..] : content;
            text = new UTF8Encoding(false, true).GetString(body);
        }
        catch (DecoderFallbackException error)
        {
            throw Invalid(fileName, archiveName, culture, error);
        }
        string? line = null;
        foreach (string candidate in text.Split('\n'))
        {
            string trimmed = candidate.Trim();
            if (trimmed.Length == 0) continue;
            line = trimmed;
            break;
        }
        if (line is null) throw Invalid(fileName, archiveName, culture);
        // PowerShell's -split '\s+', 2: the hash, then the rest of the line after the first whitespace.
        int split = line.AsSpan().IndexOfAny(Whitespace);
        string hash = split < 0 ? line : line[..split];
        if (hash.Length != 64 || hash.AsSpan().ContainsAnyExcept(ReleaseReader.HexDigits)) throw Invalid(fileName, archiveName, culture);
        if (split >= 0 && !string.Equals(line[split..].TrimStart().TrimStart('*'), archiveName, StringComparison.Ordinal))
            throw Invalid(fileName, archiveName, culture);
        return hash;
    }

    private static AdoResponseFormatException Invalid(string fileName, string archiveName, CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.UpdateChecksumFileInvalid, culture, fileName, archiveName), error) { Operation = UpdateHttp.DownloadOperation };
}
