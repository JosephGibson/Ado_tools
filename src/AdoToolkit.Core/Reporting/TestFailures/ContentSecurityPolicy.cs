using System.Security.Cryptography;
using System.Text;

namespace AdoToolkit.Core.Reporting.TestFailures;

public static class ContentSecurityPolicy
{
    public static string Hash(ReadOnlySpan<byte> scriptBytes) => Convert.ToBase64String(SHA256.HashData(scriptBytes));

    public static string Create(IReadOnlyList<string> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        return "default-src 'none'; script-src " + string.Join(" ", scripts.Select(script => "'sha256-" + Hash(Encoding.UTF8.GetBytes(script)) + "'"))
            + "; style-src 'unsafe-inline'; img-src 'self' data:; base-uri 'none'; form-action 'none'";
    }
}
