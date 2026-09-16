using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.IO;

public static class ReportFileNames
{
    public static string BuildLog(int buildId, int logId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logId);
        return "Build-" + buildId.ToString(CultureInfo.InvariantCulture) + "-Log-" + logId.ToString(CultureInfo.InvariantCulture) + ".txt";
    }

    public static string TestCase(int id, ReportFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return "TestCase-" + id.ToString(CultureInfo.InvariantCulture) + "-Steps." + Extension(format);
    }

    public static string TestSuite(int suiteId, ReportFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(suiteId);
        return "TestSuite-" + suiteId.ToString(CultureInfo.InvariantCulture) + "-Steps." + Extension(format);
    }

    public static string TestFailures(int buildId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        return "Build-" + buildId.ToString(CultureInfo.InvariantCulture) + "-TestFailures.html";
    }

    // §13.4 step 1: UTC, invariant culture, chosen once before rendering.
    public static string GenerationStamp(DateTimeOffset generatedAt) =>
        generatedAt.UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture);

    public static string AttachmentFolder(string baseName, string stamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseName);
        if (!IsStamp(stamp)) throw new ArgumentException(null, nameof(stamp));
        return baseName + FolderInfix + stamp;
    }

    // A generation folder is "<base>.files-" followed exactly by a real stamp. Windows names are
    // case-insensitive, so the base compares ignoring case; the stamp itself is exact.
    public static bool IsAttachmentFolder(string name, string baseName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(baseName);
        int prefix = baseName.Length + FolderInfix.Length;
        return name.Length == prefix + StampLength
            && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
            && string.CompareOrdinal(name, baseName.Length, FolderInfix, 0, FolderInfix.Length) == 0
            && IsStamp(name[prefix..]);
    }

    // Toolkit-generated only (§13.2): remote file names never reach a path.
    public static string Attachment(int runId, int resultId, int? subResultId, int attachmentId, string extension)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resultId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attachmentId);
        if (subResultId <= 0) throw new ArgumentOutOfRangeException(nameof(subResultId));
        if (extension is not (".png" or ".json" or ".html" or ".bin")) throw new ArgumentOutOfRangeException(nameof(extension));
        return "r" + runId.ToString(CultureInfo.InvariantCulture) + "-" + resultId.ToString(CultureInfo.InvariantCulture)
            + (subResultId.HasValue ? "-s" + subResultId.Value.ToString(CultureInfo.InvariantCulture) : "")
            + "-a" + attachmentId.ToString(CultureInfo.InvariantCulture) + extension;
    }

    private const string StampFormat = "yyyyMMdd'T'HHmmssfff'Z'";
    private const int StampLength = 19;
    private const string FolderInfix = ".files-";

    private static bool IsStamp(string? value) =>
        value is { Length: StampLength } && value[8] == 'T' && value[^1] == 'Z'
        && !value.AsSpan(0, 8).ContainsAnyExceptInRange('0', '9')
        && !value.AsSpan(9, 9).ContainsAnyExceptInRange('0', '9')
        && DateTime.TryParseExact(value, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    // The timestamp keeps the offset it was captured with (local time) and uses invariant digits.
    public static string TestCases(DateTimeOffset generatedAt, ReportFormat format) =>
        "TestCases-" + generatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "." + Extension(format);

    // §13.2: one case; all cases from one suite; anything else.
    public static string ForCases(IReadOnlyList<AdoTestCase> cases, DateTimeOffset generatedAt, ReportFormat format)
    {
        ArgumentNullException.ThrowIfNull(cases);
        if (cases.Count == 0) throw new ArgumentOutOfRangeException(nameof(cases));
        if (cases.Count == 1) return TestCase(cases[0].Id, format);
        int? suiteId = cases[0].Suite?.SuiteId;
        return suiteId.HasValue && cases.All(item => item.Suite?.SuiteId == suiteId)
            ? TestSuite(suiteId.Value, format) : TestCases(generatedAt, format);
    }

    // The cmdlet supplies a resolved FileSystem path; this layer never interprets providers or wildcards.
    public static string Resolve(string? path, int id, ReportFormat format, CultureInfo culture,
        Func<string>? getDownloads = null) => Resolve(path, TestCase(id, format), culture, getDownloads);

    public static string Resolve(string? path, string defaultName, CultureInfo culture, Func<string>? getDownloads = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);
        ArgumentNullException.ThrowIfNull(culture);
        string destination;
        if (path is null)
        {
            string directory = getDownloads is not null ? getDownloads()
                : OperatingSystem.IsWindows() ? KnownFolders.Downloads(culture)
                : throw new AdoFileOutputException(Messages.Get(AdoMessage.DownloadsUnavailable, culture));
            destination = Path.Combine(directory, defaultName);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            destination = Directory.Exists(path) ? Path.Combine(path, defaultName) : path;
        }
        string fullPath = Path.GetFullPath(destination);
        if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, fullPath));
        return fullPath;
    }

    private static string Extension(ReportFormat format) => format switch
    {
        ReportFormat.Html => "html",
        ReportFormat.Markdown => "md",
        ReportFormat.Json => "json",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
