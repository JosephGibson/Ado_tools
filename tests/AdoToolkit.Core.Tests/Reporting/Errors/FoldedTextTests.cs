using AdoToolkit.Core.Reporting.Errors;

namespace AdoToolkit.Core.Tests.Reporting.Errors;

// Folding serves comparison only: case, accents, typographic apostrophes and runs of spaces do not
// count, and every part found in the folded text maps back to the text as the server sent it.
public sealed class FoldedTextTests
{
    [Theory]
    [InlineData("Panier vide", "PANIER VIDE")]
    [InlineData("Créer la commande", "Creer la commande")]
    [InlineData("L'élément", "L’élément")]
    [InlineData("Attendu : <1>", "Attendu : <1>")]
    [InlineData("Attendu : <1>", "Attendu :  <1>")]
    [InlineData("  Échec   de\tAssert.IsTrue. ", "echec de assert.istrue.")]
    public void CaseAccentsApostrophesAndSpacesFoldAlike(string first, string second) =>
        Assert.Equal(FoldedText.Of(first).Text, FoldedText.Of(second).Text);

    [Theory]
    [InlineData("Expected:<Welcome>", "Expected:<Cart>")]
    [InlineData("Élément #submit", "Élément #cancel")]
    public void OtherWordsStayApart(string first, string second) =>
        Assert.NotEqual(FoldedText.Of(first).Text, FoldedText.Of(second).Text);

    [Fact]
    public void ASliceOfTheFoldedTextIsTheTextAsSent()
    {
        FoldedText line = FoldedText.Of("  Échec de Assert.AreEqual. Attendu : <Bienvenue>, Réel : <Erreur>. ");
        Assert.Equal("echec de assert.areequal. attendu : <bienvenue>, reel : <erreur>.", line.Text);
        int start = line.Text.IndexOf("bienvenue", StringComparison.Ordinal);
        Assert.Equal("Bienvenue", line.Slice(start, "bienvenue".Length));
        start = line.Text.IndexOf("reel", StringComparison.Ordinal);
        Assert.Equal("Réel", line.Slice(start, 4));
        // A run of spaces folds to one, and its slice is the whole run as sent.
        start = line.Text.IndexOf(": <b", StringComparison.Ordinal);
        Assert.Equal(" : <B", line.Slice(start - 1, 5));
        Assert.Equal(string.Empty, line.Slice(3, 0));
    }

    [Fact]
    public void SurrogatePairsStayWholeAndALoneHalfIsKept()
    {
        FoldedText pair = FoldedText.Of("Logo 😀 manquant");
        int start = pair.Text.IndexOf('\uD83D', StringComparison.Ordinal);
        Assert.Equal("😀", pair.Slice(start, 2));
        FoldedText lone = FoldedText.Of("Bad \uD800 half");
        Assert.Equal("bad \uD800 half", lone.Text);
    }

    // A noncharacter that normalization refuses, such as U+FFFE, is compared as it came: remote text,
    // or a configured pattern, must not stop the export.
    [Fact]
    public void ANoncharacterIsKeptAsItCame()
    {
        FoldedText line = FoldedText.Of("Bad ￾ Value é");
        Assert.Equal("bad ￾ value e", line.Text);
        Assert.Equal("￾", line.Slice(4, 1));
        Assert.True(new ErrorPattern("*￾*").IsMatch(line.Text));
    }
}
