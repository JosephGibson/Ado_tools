using AdoToolkit.Core.Reporting.Errors;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.Errors;

// Classes and profiles over a build with an English stage and a French stage. Two forms are one error
// when one test failed with them at the same place in an English group and a French group, a group's
// language being the one its MSTest texts show; never when a group saw both forms.
public sealed class ErrorClassifierTests
{
    private static readonly Uri Collection = new("https://ado.example.test/Collection/");
    private const int English = 301, French = 302;
    private const string Welcome = "Assert.AreEqual failed. Expected:<Welcome>. Actual:<Error>. ";
    private const string Bienvenue = "Échec de Assert.AreEqual. Attendu : <Bienvenue>, Réel : <Erreur>. ";
    private const string Panier = "Échec de Assert.AreEqual. Attendu : <Panier>, Réel : <Erreur>. ";
    private const string Hello = "Assert.AreEqual failed. Expected:<Hello>. Actual:<Error>. ";

    // M1: the English and French forms of one assertion, from one test at one place, are one error;
    // another test that failed in French only joins it.
    [Fact]
    public void OneTestFailingAtOnePlaceInBothLanguagesJoinsTheForms()
    {
        ErrorClassification result = Classify(
            Test("SubmitOrder", Attempt(1, English, Welcome, Trace("HomePage.CheckTitle", 42)), Attempt(2, French, Bienvenue, Trace("HomePage.CheckTitle", 42, true))),
            Test("ShowBanner", Attempt(1, French, Bienvenue, Trace("HomePage.CheckTitle", 42, true))));
        ErrorClass error = Assert.Single(result.Classes);
        Assert.Equal(2, error.Forms.Count);
        Assert.Equal(new ErrorPairing(0, 0, 1), Assert.Single(error.Pairings));
        Assert.All(result.Profiles, profile => Assert.Same(error, profile.Primary!.Class));
        Assert.Equal(2, result.Profiles[0].Primary!.Count);
        Assert.Equal([0, 1], result.Profiles[0].Primary!.Groups);
    }

    // M3 and M7b: forms the catalog does not know join the same way, when the groups' MSTest wrapper
    // texts say which language each is.
    [Theory]
    [InlineData("Synthetic.Web.HomePageException: Home page did not load within 30 seconds",
        "Synthetic.Web.HomePageException: La page d’accueil ne s’est pas chargée en moins de 30 secondes")]
    [InlineData("System.NullReferenceException: Object reference not set to an instance of an object.",
        "System.NullReferenceException: La référence d'objet n'est pas définie à une instance d'un objet.")]
    public void FormsOutsideTheCatalogJoinThroughTheWrapperLanguage(string english, string french)
    {
        ErrorClassification result = Classify(Test("OpenHome",
            Attempt(1, English, "Test method Synthetic.Web.OpenHome threw exception: \r\n" + english, Trace("HomePage.Open", 12)),
            Attempt(2, French, "La méthode de test Synthetic.Web.OpenHome a levé une exception : \r\n" + french, Trace("HomePage.Open", 12, true))));
        Assert.Equal(2, Assert.Single(result.Classes).Forms.Count);
    }

    [Fact]
    public void TwoFormsOfOneGroupNeverJoin()
    {
        // A3: two French tests, two expected values: no test failed with both, and they share a group.
        ErrorClassification apart = Classify(
            Test("ShowBanner", Attempt(1, French, Bienvenue, Trace("HomePage.CheckTitle", 42, true))),
            Test("AddItem", Attempt(1, French, Panier, Trace("CartPage.CheckTitle", 7, true))));
        Assert.Equal(2, apart.Classes.Count);
        // A4: once Welcome and Bienvenue are one error, Hello cannot join through Bienvenue, because
        // the English group would then hold Welcome and Hello.
        ErrorClassification refused = Classify(
            Test("SubmitOrder", Attempt(1, English, Welcome, Trace("HomePage.CheckTitle", 42)), Attempt(2, French, Bienvenue, Trace("HomePage.CheckTitle", 42, true))),
            Test("Greet", Attempt(1, English, Hello, Trace("HomePage.CheckTitle", 42)), Attempt(2, French, Bienvenue, Trace("HomePage.CheckTitle", 42, true))));
        Assert.Equal([2, 1], refused.Classes.Select(static error => error.Forms.Count));
        // A9: an English text seen in the French group too is not the English form of a French one.
        const string Selenium = "OpenQA.Selenium.WebDriverException: element click intercepted";
        ErrorClassification neutral = Classify(
            Test("Pay", Attempt(1, English, Welcome, Trace("PayPage.Check", 3)), Attempt(2, French, Bienvenue, Trace("PayPage.Check", 3, true))),
            Test("Ship", Attempt(1, French, Selenium, Trace("ShipPage.Open", 9, true))),
            Test("Track", Attempt(1, English, Selenium, Trace("TrackPage.Open", 5)), Attempt(2, French, "Synthetic.Web.TrackException: Colis introuvable", Trace("TrackPage.Open", 5, true))));
        Assert.Contains(neutral.Classes, static error => error.Forms.Count == 1 && error.Forms[0].Line == Selenium);
    }

    [Fact]
    public void FormsOfDifferentPlacesOrUnknownLanguagesNeverJoin()
    {
        // A10: the English timeout and the French assertion fail at two places.
        ErrorClassification places = Classify(Test("SubmitOrder",
            Attempt(1, English, "System.TimeoutException: Timed out waiting for #submit", Trace("CheckoutPage.Submit", 88)),
            Attempt(2, French, Bienvenue, Trace("HomePage.CheckTitle", 42, true))));
        Assert.Equal(2, places.Classes.Count);
        // A13: two English jobs are two groups of one language.
        AdoTestRun[] jobs = [Run(401, "Tests", "Job_A"), Run(402, "Tests", "Job_B")];
        ErrorClassification english = Classify(jobs, Test("SubmitOrder",
            Attempt(1, 401, Welcome, Trace("HomePage.CheckTitle", 42)), Attempt(2, 402, Hello, Trace("HomePage.CheckTitle", 42))));
        Assert.Equal(2, english.Classes.Count);
        // A14: without an MSTest text the French group's language is unknown.
        ErrorClassification unknown = Classify(Test("OpenHome",
            Attempt(1, English, "Test method Synthetic.Web.OpenHome threw exception: \r\nSynthetic.Web.HomePageException: Home page did not load", Trace("HomePage.Open", 12)),
            Attempt(2, French, "Synthetic.Web.HomePageException: La page d'accueil ne s'est pas chargée", Trace("HomePage.Open", 12, true))));
        Assert.Equal(2, unknown.Classes.Count);
        // A build without groups never joins.
        ErrorClassification single = Classify([Run(English, "Tests_EN")], Test("SubmitOrder",
            Attempt(1, English, Welcome, Trace("HomePage.CheckTitle", 42)), Attempt(2, English, Bienvenue, Trace("HomePage.CheckTitle", 42))));
        Assert.Equal(2, single.Classes.Count);
    }

    [Fact]
    public void ThePrimaryErrorIsTheMostFrequentOneThatIsNotGeneric()
    {
        ErrorRule generic = new("Home page did not load", ["*Home page did not load*"], true);
        const string Home = "Synthetic.Web.HomePageException: Home page did not load";
        ErrorClassification result = Classify([generic],
            // A, A, B: A, two of three.
            Test("A", Attempt(1, English, Welcome), Attempt(2, English, Welcome), Attempt(3, English, Hello)),
            // A tie goes to the latest attempt.
            Test("B", Attempt(1, English, Welcome), Attempt(2, English, Hello)),
            // Three generic and one specific: the specific one.
            Test("C", Attempt(1, English, Home), Attempt(2, English, Home), Attempt(3, English, Home), Attempt(4, English, Welcome)),
            // Generic errors only: the most frequent of them.
            Test("D", Attempt(1, English, Home), Attempt(2, English, Home), Attempt(3, English, "System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it")),
            // No failed attempt has a message: the latest attempt with one, which did not fail.
            Test("E", Attempt(1, English, null), Attempt(2, English, "Banner shown", failed: false)),
            // No message at all.
            Test("F", Attempt(1, English, null)),
            // Flaky: the failed attempt counts, not the pass after it.
            Test("G", Attempt(1, English, Hello), Attempt(2, English, null, failed: false)));
        ErrorProfile[] profiles = [.. result.Profiles];
        Assert.Equal((Welcome.TrimEnd(), 2, 3, false), Summary(profiles[0]));
        Assert.Equal((Hello.TrimEnd(), 1, 2, true), Summary(profiles[1]));
        Assert.Equal((Welcome.TrimEnd(), 1, 4, false), Summary(profiles[2]));
        Assert.Equal(["Home page did not load", "ConnectionRefused"], profiles[2].Entries.Concat(profiles[3].Entries).Select(static entry => entry.Class.Rule?.Name).OfType<string>().Distinct());
        Assert.True(profiles[3].Primary!.Class.IsGeneric);
        Assert.Equal(2, profiles[3].Primary!.Count);
        Assert.False(profiles[4].Primary!.Latest.Failed);
        Assert.Equal(1, profiles[4].FailedAttempts);
        Assert.Null(profiles[5].Primary);
        Assert.Empty(profiles[5].Entries);
        Assert.Equal((Hello.TrimEnd(), 1, 1, false), Summary(profiles[6]));
        Assert.Equal([1], profiles[0].Others.Select(static entry => entry.Count));
    }

    // The same tests give the same classes, in the same order, every time.
    [Fact]
    public void ClassificationIsDeterministic()
    {
        AdoTestFailure[] tests = [.. Enumerable.Range(0, 40).Select(index => Test("T" + index.ToString(CultureInfo.InvariantCulture),
            Attempt(1, English, index % 3 == 0 ? Welcome : Hello, Trace("Page" + (index % 5).ToString(CultureInfo.InvariantCulture) + ".Check", 10)),
            Attempt(2, French, index % 2 == 0 ? Bienvenue : Panier, Trace("Page" + (index % 5).ToString(CultureInfo.InvariantCulture) + ".Check", 10, true))))];
        string First() => string.Join("\n", Classify(tests).Classes.Select(static error => string.Join("|", error.Forms.Select(static form => form.Key))));
        Assert.Equal(First(), First());
    }

    // 200 failures of 14 attempts with distinct messages: one read per message, nothing quadratic.
    [Fact]
    public void TwoHundredFailuresOfFourteenAttemptsAreClassified()
    {
        AdoTestFailure[] tests = [.. Enumerable.Range(0, 200).Select(test => Test("T" + test.ToString(CultureInfo.InvariantCulture),
            [.. Enumerable.Range(1, 14).Select(number => Attempt(number, number <= 7 ? English : French,
                (number <= 7 ? "Assert.AreEqual failed. Expected:<Title " : "Échec de Assert.AreEqual. Attendu : <Titre ") + test.ToString(CultureInfo.InvariantCulture)
                + (number <= 7 ? ">. Actual:<Error>. Step " : ">, Réel : <Erreur>. Étape ") + number.ToString(CultureInfo.InvariantCulture),
                Trace("Page" + test.ToString(CultureInfo.InvariantCulture) + ".Check", number, number > 7)))]))];
        ErrorClassification result = Classify(tests);
        Assert.Equal(200, result.Profiles.Count);
        Assert.All(result.Profiles, static profile => Assert.Equal(14, profile.FailedAttempts));
    }

    private static (string Line, int Count, int Failed, bool Tie) Summary(ErrorProfile profile) =>
        (profile.Primary!.Class.Forms[0].Line, profile.Primary.Count, profile.FailedAttempts, profile.IsTie);

    // When pairing joins two wordings that two rules named, the rule that comes first in the list
    // names the error, a configured one before a built-in one, whichever wording came first.
    [Fact]
    public void TheFirstRuleNamesAnErrorThatPairingJoined()
    {
        ErrorRule refused = new("Refused here", ["*Aucune connexion*"], false);
        ErrorClassification result = Classify([refused], Test("OpenHome",
            Attempt(1, English, "Test method Synthetic.Web.OpenHome threw exception: \r\nSystem.Net.Sockets.SocketException: "
                + "No connection could be made because the target machine actively refused it.", Trace("HomePage.Open", 12)),
            Attempt(2, French, "La méthode de test Synthetic.Web.OpenHome a levé une exception : \r\nSystem.Net.Sockets.SocketException: "
                + "Aucune connexion n’a pu être établie, synthetic wording", Trace("HomePage.Open", 12, true))));
        ErrorClass error = Assert.Single(result.Classes);
        Assert.Equal(2, error.Forms.Count);
        Assert.Same(refused, error.Rule);
        Assert.False(error.IsGeneric);
    }

    private static ErrorClassification Classify(params AdoTestFailure[] failures) => Classify([Run(English, "Tests_EN"), Run(French, "Tests_FR")], failures);

    private static ErrorClassification Classify(IReadOnlyList<ErrorRule> rules, params AdoTestFailure[] failures) =>
        ErrorClassifier.Classify(failures, PipelineGrouping.Create([Run(English, "Tests_EN"), Run(French, "Tests_FR")]), [.. rules, .. BuiltInErrorRules.All]);

    private static ErrorClassification Classify(AdoTestRun[] runs, params AdoTestFailure[] failures) =>
        ErrorClassifier.Classify(failures, PipelineGrouping.Create(runs), BuiltInErrorRules.All);

    private static AdoTestRun Run(int id, string stage, string? job = null) => new()
    {
        Id = id, Name = "UI tests", BuildId = 401, State = "Completed", StageName = stage, PhaseName = job, TeamProject = "Shop", CollectionUri = Collection,
        StartedDate = new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero).AddMinutes(id),
    };

    private static AdoTestAttempt Attempt(int number, int run, string? message, string? trace = null, bool failed = true) => new()
    {
        Number = number, RunId = run, ResultId = 10 + number, Outcome = failed ? "Failed" : "Passed",
        OutcomeClass = failed ? AdoTestOutcomeClass.Failure : AdoTestOutcomeClass.Pass, ErrorMessage = message, StackTrace = trace,
    };

    private static AdoTestFailure Test(string name, params AdoTestAttempt[] attempts) => new()
    {
        Ordinal = 1, ShortName = name, TestName = "Synthetic.Web.Tests." + name, Classification = AdoTestFailureClassification.Failed,
        CollectionUri = Collection, Attempts = attempts,
    };

    // A frame of the test's own code, in English or in French as a French .NET Framework writes it.
    private static string Trace(string method, int line, bool french = false) => french
        ? "   à Synthetic.Web.Pages." + method + "() dans C:\\agent\\_work\\1\\s\\src\\Pages.cs:ligne " + line.ToString(CultureInfo.InvariantCulture)
        : "   at Synthetic.Web.Pages." + method + "() in C:\\agent\\_work\\1\\s\\src\\Pages.cs:line " + line.ToString(CultureInfo.InvariantCulture);
}
