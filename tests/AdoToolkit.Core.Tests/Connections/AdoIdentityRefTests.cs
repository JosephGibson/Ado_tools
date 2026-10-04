namespace AdoToolkit.Core.Tests.Connections;

[Trait("Culture", "Invariant")]
public sealed class AdoIdentityRefTests
{
    // An identity held as a field value is shown through ToString: a PowerShell table cell of
    // AdoWorkItem.Fields, or a custom field in a report. It must not be the CLR type name.
    [Theory]
    [InlineData("Fictional Person", "person@example.test", "Fictional Person <person@example.test>")]
    [InlineData("Fictional Person", null, "Fictional Person")]
    [InlineData("Équipe « Web »", "", "Équipe « Web »")]
    public void TextIsTheDisplayNameThenTheUniqueName(string displayName, string? uniqueName, string expected) =>
        Assert.Equal(expected, new AdoIdentityRef { Id = "opaque-id", DisplayName = displayName, UniqueName = uniqueName }.ToString());
}
