using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

public sealed class ErrorTranslationTests
{
    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(401, "iis-unauthenticated.html.txt", typeof(AdoAuthenticationException))]
    [InlineData(403, "forbidden.json", typeof(AdoAuthorizationException))]
    [InlineData(404, "not-found.json", typeof(AdoNotFoundException))]
    [InlineData(400, "bad-version.json", typeof(AdoRequestException))]
    [InlineData(409, "forbidden.json", typeof(AdoRequestException))]
    [InlineData(412, "forbidden.json", typeof(AdoRequestException))]
    [InlineData(302, "redirect.json", typeof(AdoRedirectException))]
    public async Task StatusDeterminesExceptionAndErrorsDoNotFollowRedirects(int status, string fixture, Type expected)
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = FakeHttpMessageHandler.Fixture(fixture, status);
        if (status == 302) response.Headers.Location = new Uri("https://sample:synthetic@redirect.example.test/target?private=value#fragment");
        handler.Enqueue(response);
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5));
        Exception? caught = await Record.ExceptionAsync(() => pipeline.GetPagesAsync(EndpointRegistry.ProjectsList,
            AdoJsonContext.Default.ProjectPageDto, static page => page.Value, static item => item.Id ?? "",
            CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken));
        AdoException error = Assert.IsAssignableFrom<AdoException>(caught);
        Assert.Equal(expected, error.GetType());
        Assert.Equal(status, error.StatusCode);
        Assert.Equal("ProjectsList", error.Operation);
        Assert.False(error.IsRetryable);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("WINDOWS LOGIN FORM", error.Message, StringComparison.Ordinal);
        if (status == 302)
        {
            Assert.Contains("https://redirect.example.test/target", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("private", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("fragment", error.Message, StringComparison.Ordinal);
        }
        if (status == 400) Assert.Contains("registre", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(401, "<html>NEVER ECHO</html>", "text/html", typeof(AdoAuthenticationException))]
    [InlineData(403, "{broken", "application/json", typeof(AdoAuthorizationException))]
    [InlineData(404, "{\"message\":42,\"typeKey\":false}", "application/json", typeof(AdoNotFoundException))]
    [InlineData(403, "[]", "application/json", typeof(AdoAuthorizationException))]
    [InlineData(403, "<html>NEVER ECHO</html>", "text/html", typeof(AdoAuthorizationException))]
    public async Task MissingOrMalformedErrorFieldsDoNotHideKnownStatus(int status, string body, string media, Type expected)
    {
        using HttpResponseMessage response = FakeHttpMessageHandler.Response(body, status, media);
        AdoException error = await ErrorTranslator.TranslateAsync(response, EndpointRegistry.ProjectsList, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(expected, error.GetType());
        Assert.Null(error.CorrelationId);
        Assert.DoesNotContain("NEVER ECHO", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    [Trait("Acceptance", "S0-7")]
    public async Task PreservesFrenchServerTextAndAppendsFrenchToolkitHint()
    {
        using HttpResponseMessage response = FakeHttpMessageHandler.Fixture("french-error.json", 404);
        response.Headers.TryAddWithoutValidation("ActivityId", "sample\r\ncorrelation");
        AdoException error = await ErrorTranslator.TranslateAsync(response, EndpointRegistry.ProjectsList, CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        Assert.Contains("La ressource demandée est introuvable.", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', error.CorrelationId!);
        Assert.DoesNotContain('\n', error.CorrelationId!);
        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public void BoundsAndSanitizesRemoteText()
    {
        string remote = "<b>sample</b>\r\n" + new string('x', 5000);
        string result = ErrorTranslator.Sanitize(remote, 1024)!;
        Assert.Equal(1024, result.Length);
        Assert.DoesNotContain("<b>", result, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('\n', result);
    }
}
