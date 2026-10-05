using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Update;

// Reads GitHub's description of the latest release. Every value in it is remote text: the version
// comes from the tag alone, and the assets are looked up by names built from that version.
internal static partial class ReleaseReader
{
    private const string DigestPrefix = "sha256:";
    private const int TagLength = 64;
    internal static readonly System.Buffers.SearchValues<char> HexDigits = System.Buffers.SearchValues.Create("0123456789abcdefABCDEF");

    internal sealed record Asset(string Name, long Size, string Sha256);

    internal sealed record Release(Version Version, Asset Archive, Asset Checksum);

    internal static Release Read(ReadOnlyMemory<byte> json, AdoToolkitInstallMode mode, UpdateHttp.Limits limits, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(culture);
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException error) { throw Format(culture, error); }
        using (document)
            // A string that cannot be read, such as a lone surrogate escape, is an InvalidOperationException.
            try { return Read(document.RootElement, mode, limits, culture); }
            catch (InvalidOperationException error) { throw Format(culture, error); }
    }

    private static Release Read(JsonElement root, AdoToolkitInstallMode mode, UpdateHttp.Limits limits, CultureInfo culture)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("tag_name", out JsonElement tag) || tag.ValueKind != JsonValueKind.String
            || !IsFalse(root, "draft") || !IsFalse(root, "prerelease")
            || !root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            throw Format(culture);
        string text = tag.GetString() ?? "";
        Version version = ParseTag(text)
            ?? throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateTagInvalid, culture, UpdateGuards.Printable(text, TagLength)))
            { Operation = UpdateHttp.LatestOperation };
        string archive = UpdateHttp.ArchiveName(version, mode);
        long limit = mode == AdoToolkitInstallMode.Portable ? limits.PortableArchiveBytes : limits.ModuleArchiveBytes;
        return new Release(version, Find(assets, archive, limit, version, culture),
            Find(assets, archive + ".sha256", limits.ChecksumBytes, version, culture));
    }

    // The developer's ^v\d+\.\d+\.\d+$, made stricter: ASCII digits, because \d matches the digits of
    // other scripts; \z, because $ accepts a final newline; no leading zero; at most nine digits a part.
    [GeneratedRegex(@"^v(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\z", RegexOptions.CultureInvariant, 100)]
    private static partial Regex TagPattern();

    internal static Version? ParseTag(string? tag)
    {
        if (tag is null) return null;
        Match match;
        try { match = TagPattern().Match(tag); }
        catch (RegexMatchTimeoutException) { return null; }
        if (!match.Success) return null;
        return new Version(Part(match, 1), Part(match, 2), Part(match, 3));
    }

    private static int Part(Match match, int group) => int.Parse(match.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);

    private static bool IsFalse(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.False;

    private static Asset Find(JsonElement assets, string name, long limit, Version version, CultureInfo culture)
    {
        Asset? found = null;
        foreach (JsonElement item in assets.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out JsonElement itemName)
                || itemName.ValueKind != JsonValueKind.String) throw Format(culture);
            if (!string.Equals(itemName.GetString(), name, StringComparison.Ordinal)) continue;
            // Two assets with one name cannot both be what the release describes.
            if (found is not null) throw Format(culture);
            if (!item.TryGetProperty("size", out JsonElement size) || size.ValueKind != JsonValueKind.Number
                || !size.TryGetInt64(out long bytes) || bytes < 1) throw Format(culture);
            if (bytes > limit)
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateAssetTooLarge, culture, name, bytes, limit))
                { Operation = UpdateHttp.LatestOperation };
            string? digest = item.TryGetProperty("digest", out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (!IsSha256Digest(digest))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateDigestMissing, culture, name)) { Operation = UpdateHttp.LatestOperation };
            found = new Asset(name, bytes, digest![DigestPrefix.Length..]);
        }
        return found ?? throw new AdoNotFoundException(Messages.Get(AdoMessage.UpdateAssetMissing, culture, UpdateHttp.VersionText(version), name))
        { Operation = UpdateHttp.LatestOperation };
    }

    private static bool IsSha256Digest(string? digest) =>
        digest is not null && digest.Length == DigestPrefix.Length + 64 && digest.StartsWith(DigestPrefix, StringComparison.Ordinal)
        && !digest.AsSpan(DigestPrefix.Length).ContainsAnyExcept(HexDigits);

    private static AdoResponseFormatException Format(CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.UpdateReleaseFormat, culture), error) { Operation = UpdateHttp.LatestOperation };
}
