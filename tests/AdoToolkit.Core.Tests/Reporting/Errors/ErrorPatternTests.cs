using AdoToolkit.Core.Reporting.Errors;

namespace AdoToolkit.Core.Tests.Reporting.Errors;

// The wildcards of reporting.errorRules: * is any text and ? one character, a pattern matches a whole
// line, and case, accents, apostrophes and spaces do not count.
public sealed class ErrorPatternTests
{
    [Theory]
    [InlineData("Home page did not load within * seconds", "Home page did not load within 30 seconds")]
    [InlineData("La page d'accueil ne s'est pas chargée en moins de * secondes", "LA PAGE D’ACCUEIL NE S’EST PAS CHARGEE EN MOINS DE 30 SECONDES")]
    [InlineData("*Expected:<Welcome*", "Assert.AreEqual failed. Expected:<Welcome>. Actual:<Error>.")]
    [InlineData("Timed out after ?? seconds", "Timed out after 30 seconds")]
    [InlineData("*", "anything at all")]
    [InlineData("a*b*c", "a--b--c")]
    [InlineData("a*b*c", "abc")]
    [InlineData("*b?d*", "abxdz")]
    [InlineData("exact line", "Exact   line")]
    public void APatternMatchesTheWholeFoldedLine(string pattern, string line) =>
        Assert.True(new ErrorPattern(pattern).IsMatch(FoldedText.Of(line).Text));

    [Theory]
    // Whole line: without a star the pattern must reach both ends.
    [InlineData("Home page did not load", "Home page did not load within 30 seconds")]
    [InlineData("did not load within * seconds", "Home page did not load within 30 seconds")]
    [InlineData("Timed out after ?? seconds", "Timed out after 300 seconds")]
    [InlineData("a*b*c", "a--c--b")]
    [InlineData("*b?d*", "abd")]
    [InlineData("Home page did not load within * seconds", "Home page did load within 30 seconds")]
    public void ANearMissDoesNotMatch(string pattern, string line) =>
        Assert.False(new ErrorPattern(pattern).IsMatch(FoldedText.Of(line).Text));

    // A segment longer than one 64-bit word, with wildcards, crosses the word boundary of the
    // shift-and search.
    [Fact]
    public void ALongSegmentWithWildcardsMatchesAcrossWords()
    {
        string segment = string.Concat(Enumerable.Range(0, 90).Select(static index => index % 7 == 0 ? '?' : (char)('a' + index % 26)));
        string line = string.Concat(segment.Select(static (c, index) => c == '?' ? 'z' : c));
        Assert.True(new ErrorPattern("*" + segment + "*").IsMatch("prefix " + line + " suffix"));
        Assert.False(new ErrorPattern("*" + segment + "*").IsMatch("prefix " + line[..^1] + "#" + " suffix"));
    }

    // A message of a megabyte on one line: the match reads it once.
    [Fact]
    public void AVeryLongLineIsMatchedInOnePass()
    {
        string line = new string('x', 1 << 20) + " home page did not load within 30 seconds";
        Assert.True(new ErrorPattern("*home page did not load within * seconds").IsMatch(line));
        Assert.False(new ErrorPattern("*home page did not load within ? seconds").IsMatch(line));
        Assert.False(new ErrorPattern("*connection refused*").IsMatch(line));
    }
}
