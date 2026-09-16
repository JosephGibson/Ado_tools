namespace AdoToolkit.Core.Tests.Diagnostics;

public sealed class DiagnosticMessageRendererTests
{
    [Theory]
    [InlineData(DiagnosticCodes.NotAStepContainer, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.EmptySteps, AdoDiagnosticSeverity.Info)]
    [InlineData(DiagnosticCodes.MalformedStepsXml, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.UnknownStepElement, AdoDiagnosticSeverity.Warning)]
    [InlineData(DiagnosticCodes.UnknownStepType, AdoDiagnosticSeverity.Warning)]
    [InlineData(DiagnosticCodes.UnexpectedComprefChildren, AdoDiagnosticSeverity.Warning)]
    [InlineData(DiagnosticCodes.InvalidSharedStepReference, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.UnresolvedSharedStep, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.SharedStepHasNoSteps, AdoDiagnosticSeverity.Warning)]
    [InlineData(DiagnosticCodes.CircularSharedStepReference, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.MaximumDepthExceeded, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.ExpansionLimitExceeded, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.ResolutionLimitExceeded, AdoDiagnosticSeverity.Error)]
    [InlineData(DiagnosticCodes.NestedEncodingDecoded, AdoDiagnosticSeverity.Info)]
    [InlineData(DiagnosticCodes.MalformedParameterData, AdoDiagnosticSeverity.Warning)]
    [InlineData(DiagnosticCodes.UnresolvedSharedParameter, AdoDiagnosticSeverity.Warning)]
    public void EverySliceOneCodeHasEnglishFrenchAndStableSeverity(string code, AdoDiagnosticSeverity severity)
    {
        string[] arguments = ["1234", "5"];
        AdoDiagnostic english = DiagnosticMessageRenderer.Create(code, CultureInfo.GetCultureInfo("en-US"), 42, arguments);
        string french = DiagnosticMessageRenderer.Render(english.Code, english.Arguments, CultureInfo.GetCultureInfo("fr-CA"));
        Assert.Equal(severity, english.Severity);
        Assert.NotEqual(english.Message, french);
        Assert.NotEqual(code, french);
        Assert.DoesNotContain("{0}", french, StringComparison.Ordinal);
        Assert.Equal(french, DiagnosticMessageRenderer.Render(code, arguments, CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal(arguments, english.Arguments);
    }

    [Fact]
    public void CapturesImmutableInvariantArgumentsAndReferenceChainForRerendering()
    {
        string[] arguments = ["1234", "5"];
        int[] chain = [42, 7001, 42];
        AdoDiagnostic diagnostic = DiagnosticMessageRenderer.Create(DiagnosticCodes.MalformedStepsXml,
            CultureInfo.GetCultureInfo("en-US"), 42, arguments, "3.1", chain);
        arguments[0] = "changed";
        chain[0] = 99;
        Assert.Equal("1234", diagnostic.Arguments[0]);
        Assert.Equal(42, diagnostic.ReferenceChain[0]);
        Assert.Equal("3.1", diagnostic.StepNumber);
        Assert.Equal("Steps XML is invalid at line 1234, position 5.", diagnostic.Message);
        Assert.Equal("Le XML des étapes est invalide à la ligne 1234, position 5.",
            DiagnosticMessageRenderer.Render(diagnostic.Code, diagnostic.Arguments, CultureInfo.GetCultureInfo("fr-CA")));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)diagnostic.Arguments).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)diagnostic.ReferenceChain).Clear());
    }
}
