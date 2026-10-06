using AdoToolkit.Core.Reporting.Errors;

namespace AdoToolkit.Core.Tests.Reporting.Errors;

// The form of one message: the catalog's templates in both languages, the default messages of the
// runtime, the rules, and the tokenized key line. The messages are those the frameworks produced
// under en-US and fr-FR (see ErrorTemplates), with synthetic values.
public sealed class ErrorFormsTests
{
    private const string Frame = "Synthetic.Web.Pages.CheckoutPage.Submit";

    [Theory]
    // MSTest 2.2.10 and 3.11.1.
    [InlineData("Assert.AreEqual failed. Expected:<Welcome>. Actual:<Error>. ", "MsTestAreEqual", "English")]
    [InlineData("Échec de Assert.AreEqual. Attendu : <Welcome>, Réel : <Error>. ", "MsTestAreEqual", "French")]
    [InlineData("Assert.AreEqual failed. Expected:<welcome>. Case is different for actual value:<Welcome>. ", "MsTestAreEqualCase", "English")]
    [InlineData("Échec de Assert.AreEqual. Attendu :<welcome>. La casse est différente pour la valeur réelle :<Welcome>. ", "MsTestAreEqualCase", "French")]
    [InlineData("Assert.AreNotEqual failed. Expected any value except:<Error>. Actual:<Error>. ", "MsTestAreNotEqual", "English")]
    [InlineData("Échec de Assert.AreNotEqual. Toute valeur attendue sauf :<Error>. Réel :<Error>. ", "MsTestAreNotEqual", "French")]
    [InlineData("Assert.IsInstanceOfType failed.  Expected type:<System.Int32>. Actual type:<System.String>.", "MsTestInstanceOf", "English")]
    [InlineData("Échec de Assert.IsInstanceOfType. Type attendu :<System.Int32>. Type réel :<System.String>.", "MsTestInstanceOf", "French")]
    [InlineData("StringAssert.Contains failed. String 'Error' does not contain string 'Welcome'. .", "MsTestStringAssert", "English")]
    [InlineData("Échec de StringAssert.Contains. La chaîne 'Error' ne contient pas la chaîne 'Welcome'. .", "MsTestStringAssert", "French")]
    [InlineData("Assert.IsTrue failed. Banner shown", "MsTestCondition", "English")]
    [InlineData("Échec de Assert.IsTrue. Banner shown", "MsTestCondition", "French")]
    [InlineData("Assert.AreEqual failed. Expected string length 7 but was 5.\r\nExpected: \"Welcome\"\r\nBut was:  \"Error\"\r\n-----------^", "MsTestAreEqualStrings", "English")]
    [InlineData("Échec de Assert.AreEqual. La longueur de chaîne attendue 7 mais était 5.\r\nAttendu :       \"Welcome\"\r\nMais c'était :  \"Error\"\r\n-----------------^", "MsTestAreEqualStrings", "French")]
    [InlineData("Class Initialization method Synthetic.Web.CheckoutTests.Setup threw exception. System.InvalidOperationException: Shop is closed.", "MsTestClassInitialize", "English")]
    [InlineData("La méthode de classe Initialization Synthetic.Web.CheckoutTests.Setup a levé une exception. System.InvalidOperationException : Shop is closed.", "MsTestClassInitialize", "French")]
    [InlineData("Test 'SubmitOrder' exceeded execution timeout period.", "MsTestTimeout", "English")]
    [InlineData("Le test 'SubmitOrder' a dépassé le délai d'attente de l'exécution.", "MsTestTimeout", "French")]
    [InlineData("Test 'SubmitOrder' timed out after 5000ms", "MsTestTimedOut", "English")]
    [InlineData("Le test « SubmitOrder » a expiré après 5000 ms", "MsTestTimedOut", "French")]
    // MSTest 4.4.1: the summary is in the culture's language, the labels in English.
    [InlineData("Assertion failed. Expected strings to be equal.\r\nStrings have different lengths (expected: 7, actual: 5) and differ at 1 location(s). First difference at index 0.\r\n\r\nexpected:   \"Welcome\"\r\nactual:     \"Error\"\r\ndifference: -^\r\n\r\nAssert.AreEqual(expected, actual)", "MsTest4.AreEqualStrings", "English")]
    [InlineData("Assertion failed. Les chaînes attendues doivent être égales.\r\nLes chaînes ont des longueurs différentes (attendues : 7, réelles : 5) et diffèrent à 1 emplacement(s). Première différence à l’indice 0.\r\n\r\nexpected:   \"Welcome\"\r\nactual:     \"Error\"\r\ndifference: -^\r\n\r\nAssert.AreEqual(expected, actual)", "MsTest4.AreEqualStrings", "French")]
    [InlineData("Assertion failed. Condition attendue pour être true.\r\n\r\nactual: false\r\n\r\nAssert.IsTrue(shown)", "MsTest4.IsTrue", "French")]
    [InlineData("Assertion failed. Expected value to be of type Int32 (or derived).\r\n\r\nexpected type: System.Int32 (or derived)\r\nactual type:   System.String\r\n\r\nAssert.IsInstanceOfType(actual)", "MsTest4.IsInstanceOfType", "Neutral")]
    [InlineData("Assertion failed.\r\nBoom", "MsTest4.Fail", "Neutral")]
    // xUnit 2.9.3 and v3 4.0.1, NUnit 3.14.0 and 4.6.1, Selenium 4.50.0: English only.
    [InlineData("Assert.Equal() Failure: Strings differ\r\n           ↓ (pos 0)\r\nExpected: \"Welcome\"\r\nActual:   \"Error\"\r\n           ↑ (pos 0)", "Xunit.equal", "Neutral")]
    [InlineData("Assert.Contains() Failure: Sub-string not found\r\nString:    \"Error\"\r\nNot found: \"Welcome\"", "Xunit.contains", "Neutral")]
    [InlineData("  Expected string length 7 but was 5. Strings differ at index 0.\r\n  Expected: \"Welcome\"\r\n  But was:  \"Error\"\r\n  -----------^\r\n", "NUnit.Constraint", "Neutral")]
    [InlineData("  Assert.That(actual, Is.EqualTo(expected))\r\n  Expected: 1\r\n  But was:  2\r\n", "NUnit.Constraint", "Neutral")]
    [InlineData("OpenQA.Selenium.WebDriverTimeoutException: Timed out after 30 seconds: element #submit not visible", "Selenium.WaitTimeout", "Neutral")]
    public void EachFrameworkTextIsATemplateOfItsLanguage(string message, string template, string language)
    {
        ErrorSignature form = Form(message);
        Assert.Equal(template, form.TemplateId);
        Assert.Equal(Enum.Parse<ErrorLanguage>(language), form.Language);
        Assert.Null(form.Rule);
    }

    // The English and French forms of one template line their slots up: the same values give one form.
    [Theory]
    [InlineData("Assert.AreEqual failed. Expected:<1>. Actual:<2>. ", "Échec de Assert.AreEqual. Attendu : <1>, Réel : <2>. ")]
    [InlineData("Assert.IsTrue failed. Banner shown", "Échec de Assert.IsTrue. Banner shown")]
    [InlineData("Test method A.B threw exception: \r\nSystem.InvalidOperationException: Shop is closed", "La méthode de test A.B a levé une exception : \r\nSystem.InvalidOperationException: Shop is closed")]
    [InlineData("Class Initialization method A.Setup threw exception. System.InvalidOperationException: Shop is closed.",
        "La méthode de classe Initialization A.Setup a levé une exception. System.InvalidOperationException : Shop is closed.")]
    public void TheEnglishAndFrenchFormsOfOneTemplateAreOneForm(string english, string french)
    {
        Assert.Equal(Form(english).Key, Form(french).Key);
        Assert.Equal(Form(english).Layout, Form(french).Layout);
    }

    [Theory]
    // M4: the actual value only varies.
    [InlineData("Assert.AreEqual failed. Expected:<Welcome>. Actual:<Error>. ", "Assert.AreEqual failed. Expected:<Welcome>. Actual:<Loading>. ")]
    // M5: numbers are slots.
    [InlineData("System.TimeoutException: Timed out after 3000 ms", "System.TimeoutException: Timed out after 30000 ms")]
    // M8: xUnit's actual value is on its own line.
    [InlineData("Assert.Equal() Failure: Values differ\r\nExpected: 5\r\nActual:   3", "Assert.Equal() Failure: Values differ\r\nExpected: 5\r\nActual:   4")]
    // M9: case, accents, apostrophes and spaces.
    [InlineData("L'élément Panier vide est absent", "L’ELEMENT  PANIER VIDE EST ABSENT")]
    // The same assertion in MSTest 3.x's string form, whatever the lengths.
    [InlineData("Assert.AreEqual failed. Expected string length 7 but was 5.\r\nExpected: \"Welcome\"\r\nBut was:  \"Error\"",
        "Assert.AreEqual failed. String lengths are both 7 but differ at index 0.\r\nExpected: \"Welcome\"\r\nBut was:  \"Bonsoir\"")]
    public void FormsThatDifferInValuesOnlyAreOneForm(string first, string second) => Assert.Equal(Form(first).Key, Form(second).Key);

    [Theory]
    // A1: the expected value identifies the assertion.
    [InlineData("Assert.AreEqual failed. Expected:<Welcome>. Actual:<Error>. ", "Assert.AreEqual failed. Expected:<Cart>. Actual:<Error>. ")]
    // A2: quoted text outside a template stays part of the form.
    [InlineData("OpenQA.Selenium.NoSuchElementException: no such element: Unable to locate element: {\"method\":\"css selector\",\"selector\":\"#submit\"}",
        "OpenQA.Selenium.NoSuchElementException: no such element: Unable to locate element: {\"method\":\"css selector\",\"selector\":\"#cancel\"}")]
    // A8: other text.
    [InlineData("Expected 4 results", "Expected 4 items")]
    // A11: the type is part of the line.
    [InlineData("Synthetic.FooException: boom", "Synthetic.BarException: boom")]
    // A12: the initialization method identifies the error.
    [InlineData("Class Initialization method A.Setup threw exception. System.InvalidOperationException: Shop is closed.",
        "Class Initialization method B.Setup threw exception. System.InvalidOperationException: Shop is closed.")]
    // A16: a custom message of a type that has a default one stays part of the form.
    [InlineData("System.OperationCanceledException: Simulated stop", "System.OperationCanceledException: Lock lost")]
    // The test author's message identifies the assertion; so does xUnit's expected value.
    [InlineData("Assert.IsTrue failed. Banner shown", "Assert.IsTrue failed. Footer shown")]
    [InlineData("Assert.Equal() Failure: Strings differ\r\nExpected: \"Welcome\"\r\nActual:   \"Error\"", "Assert.Equal() Failure: Strings differ\r\nExpected: \"Cart\"\r\nActual:   \"Error\"")]
    public void FormsThatDifferInWhatIdentifiesThemStayApart(string first, string second) => Assert.NotEqual(Form(first, Frame).Key, Form(second, Frame).Key);

    // A5, A6 and A7: a form with no identifying text is told apart by the test's own frame, and keys on
    // its text alone when there is no frame.
    [Theory]
    [InlineData("Assert.IsTrue failed. ")]
    [InlineData("Échec de Assert.IsTrue. ")]
    [InlineData("OpenQA.Selenium.WebDriverTimeoutException: Timed out after 30 seconds")]
    [InlineData("System.NullReferenceException: Object reference not set to an instance of an object.")]
    [InlineData("Assert.True() Failure\r\nExpected: True\r\nActual:   False")]
    [InlineData("  Expected: True\r\n  But was:  False\r\n")]
    public void AFormWithNoIdentifyingTextIsLocatedByTheOwnFrame(string message)
    {
        ErrorSignature here = Form(message, Frame), there = Form(message, "Synthetic.Web.Pages.LoginPage.Open"), nowhere = Form(message);
        Assert.True(here.IsLocated);
        Assert.NotEqual(here.Key, there.Key);
        Assert.Equal(here.FramelessKey, there.FramelessKey);
        Assert.False(nowhere.IsLocated);
        Assert.Equal(here.FramelessKey, nowhere.Key);
    }

    // MSTest 4.x and NUnit 4.x write the call as written: it tells two conditions apart without a frame.
    [Theory]
    [InlineData("Assertion failed. Expected condition to be true.\r\n\r\nactual: false\r\n\r\nAssert.IsTrue(shown)", "Assertion failed. Expected condition to be true.\r\n\r\nactual: false\r\n\r\nAssert.IsTrue(isLoggedIn)")]
    [InlineData("  Assert.That(shown, Is.True)\r\n  Expected: True\r\n  But was:  False\r\n", "  Assert.That(isLoggedIn, Is.True)\r\n  Expected: True\r\n  But was:  False\r\n")]
    public void TheCallTellsTwoConditionsApart(string first, string second)
    {
        Assert.False(Form(first, Frame).IsLocated);
        Assert.NotEqual(Form(first, Frame).Key, Form(second, Frame).Key);
    }

    // MSTest 4.x: the expected value identifies, the actual value varies, in both languages.
    [Fact]
    public void ModernAssertionsKeyOnTheExpectedValue()
    {
        const string English = "Assertion failed. Expected strings to be equal.\r\nStrings have different lengths (expected: 7, actual: 5) and differ at 1 location(s). "
            + "First difference at index 0.\r\n\r\nexpected:   \"Welcome\"\r\nactual:     \"Error\"\r\ndifference: -^\r\n\r\nAssert.AreEqual(expected, actual)";
        const string French = "Assertion failed. Les chaînes attendues doivent être égales.\r\nLes chaînes ont des longueurs différentes (attendues : 7, réelles : 6) et "
            + "diffèrent à 1 emplacement(s). Première différence à l’indice 0.\r\n\r\nexpected:   \"Welcome\"\r\nactual:     \"Erreur\"\r\ndifference: -^\r\n\r\nAssert.AreEqual(expected, actual)";
        Assert.Equal(Form(English).Key, Form(French).Key);
        Assert.NotEqual(Form(English).Key, Form(English.Replace("expected:   \"Welcome\"", "expected:   \"Cart\"", StringComparison.Ordinal)).Key);
        // The test author's message, on its own line, identifies too.
        Assert.NotEqual(Form(English).Key, Form(English.Replace("index 0.\r\n", "index 0.\r\nTitle of the home page\r\n", StringComparison.Ordinal)).Key);
    }

    // A test author's line identifies the form, so the form shows it too, and the leading exception
    // type that a heading shows apart lies on the form's own first line.
    [Theory]
    [InlineData("  Cart should be empty\r\n  Expected: 0\r\n  But was:  3\r\n", "Cart should be empty · Expected: 0 · But was:  3")]
    [InlineData("System.Net.Http.HttpRequestException: Connection refused\n  Expected: 0\n  But was:  3",
        "System.Net.Http.HttpRequestException: Connection refused · Expected: 0 · But was:  3")]
    [InlineData("Assertion failed. Expected values to differ.\r\nTitle of the home page\r\n\r\nnotExpected: \"Welcome\"\r\nactual:      \"Welcome\"\r\n\r\nAssert.AreNotEqual(title, actual)",
        "Assertion failed. Expected values to differ. · Title of the home page · notExpected: \"Welcome\" · actual:      \"Welcome\" · Assert.AreNotEqual(title, actual)")]
    public void TheLinesThatIdentifyAFormAreTheLinesItShows(string message, string line)
    {
        ErrorSignature form = Form(message);
        Assert.Equal(line, form.Line);
        Assert.True(form.PrefixLength <= form.Line.Length);
        if (form.PrefixType is { } type) Assert.StartsWith(type, form.Line, StringComparison.Ordinal);
    }

    // M7: a default message of the runtime says nothing the type does not, so the form is the type at
    // its frame; a message the test wrote stays part of the form.
    [Fact]
    public void ADefaultMessageIsTheTypeAtItsFrame()
    {
        ErrorSignature form = Form("System.NullReferenceException: Object reference not set to an instance of an object.", Frame);
        Assert.Equal("D|system.nullreferenceexception|@" + Frame, form.Key);
        Assert.Equal(form.Key, Form("System.NullReferenceException: Object reference not set to an instance of an object.", Frame).Key);
        Assert.StartsWith("X|", Form("System.NullReferenceException: Cart is null", Frame).Key, StringComparison.Ordinal);
    }

    // M10 and M11: a built-in rule makes one generic error of its texts, and reads an inner exception.
    [Theory]
    [InlineData("System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it. (selenium.example.test:4444)", "ConnectionRefused")]
    [InlineData("OpenQA.Selenium.WebDriverException: unknown error: net::ERR_CONNECTION_REFUSED", "ConnectionRefused")]
    [InlineData("OpenQA.Selenium.WebDriverException: Unexpected error.\r\n ---> System.Net.WebException: Unable to connect to the remote server\r\n ---> System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it selenium.example.test:4444", "ConnectionRefused")]
    [InlineData("System.Net.Http.HttpRequestException: No such host is known. (shop.example.test:443)", "NameResolution")]
    [InlineData("System.Net.Http.HttpRequestException: Response status code does not indicate success: 503 (Service Unavailable).", "ServerUnavailable")]
    [InlineData("System.Net.WebException: The remote server returned an error: (503) Server Unavailable.", "ServerUnavailable")]
    [InlineData("System.Net.WebException: Le serveur distant a retourné une erreur : (502) Passerelle incorrecte.", "ServerUnavailable")]
    [InlineData("OpenQA.Selenium.WebDriverException: session not created: This version of the driver supports browser version 120", "WebDriverSession")]
    [InlineData("OpenQA.Selenium.WebDriverException: The HTTP request to the remote WebDriver server for URL http://selenium.example.test:4444/session timed out after 60 seconds.", "WebDriverSession")]
    [InlineData("OpenQA.Selenium.WebDriverTimeoutException: timeout: Timed out receiving message from renderer: 29.645", "PageLoadTimeout")]
    public void ABuiltInRuleMakesOneGenericErrorOfItsTexts(string message, string rule)
    {
        ErrorSignature form = Form(message, Frame, BuiltInErrorRules.All);
        Assert.Equal(rule, form.Rule?.BuiltInId);
        Assert.True(form.Rule!.IsGeneric);
        Assert.Equal("R|b:" + rule, form.Key);
    }

    [Theory]
    // The status code alone, or in a test's own text, is no server error.
    [InlineData("Assert.AreEqual failed. Expected:<503>. Actual:<200>. ")]
    [InlineData("Expected 503 items (503) in the cart")]
    [InlineData("OpenQA.Selenium.WebDriverTimeoutException: Timed out after 30 seconds")]
    public void ABuiltInRuleLeavesOtherTextsAlone(string message) => Assert.Null(Form(message, Frame, BuiltInErrorRules.All).Rule);

    // M2: a configured rule joins its texts in every language, comes before the built-in rules, and
    // keeps the text as the server sent it.
    [Fact]
    public void AConfiguredRuleJoinsItsTextsAndComesFirst()
    {
        ErrorRule rule = new("Home page did not load", ["Home page did not load within * seconds", "La page d'accueil ne s'est pas chargée en moins de * secondes"], true);
        IReadOnlyList<ErrorRule> rules = [rule, .. BuiltInErrorRules.All];
        ErrorSignature english = Form("Synthetic.Web.HomePageException: Home page did not load within 30 seconds", Frame, rules);
        ErrorSignature french = Form("Synthetic.Web.HomePageException: La page d’accueil ne s’est pas chargée en moins de 30 secondes", Frame, rules);
        Assert.Same(rule, english.Rule);
        Assert.Equal(english.Key, french.Key);
        Assert.Equal("Synthetic.Web.HomePageException: La page d’accueil ne s’est pas chargée en moins de 30 secondes", french.Line);
        // A report applies the configured rules in file order, then the built-in ones.
        ErrorSignature first = Form("System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it.", Frame,
            BuiltInErrorRules.AfterConfigured([new ErrorRuleOptions { Name = "Refused on purpose", Patterns = ["*actively refused*"], Generic = false }]));
        Assert.Equal("Refused on purpose", first.Rule!.Name);
        Assert.Null(first.Rule.BuiltInId);
        Assert.False(first.Rule.IsGeneric);
        Assert.Equal("ConnectionRefused", Form("System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it.",
            Frame, BuiltInErrorRules.AfterConfigured([])).Rule!.BuiltInId);
    }

    // The parts of a form are the server's text, cut into slots: joined, they give the line back.
    [Theory]
    [InlineData("Échec de Assert.AreEqual. Attendu : <Bienvenue>, Réel : <Erreur>. Titre de la page")]
    [InlineData("System.TimeoutException: Timed out after 3000 ms waiting for #submit")]
    [InlineData("Le test « SubmitOrder » a expiré après 5000 ms")]
    public void ThePartsOfAOneLineFormAreTheLineAsSent(string message) => Assert.Equal(message.TrimEnd(), Form(message).Line);

    [Fact]
    public void AFormOfSeveralLinesShowsItsIdentifyingLines()
    {
        ErrorSignature form = Form("Échec de Assert.AreEqual. La longueur de chaîne attendue 9 mais était 5.\r\nAttendu :       \"Bienvenue\"\r\nMais c'était :  \"Error\"\r\n-----------------^");
        Assert.Equal("Échec de Assert.AreEqual. La longueur de chaîne attendue 9 mais était 5. · Attendu :       \"Bienvenue\" · Mais c'était :  \"Error\"", form.Line);
        Assert.Contains(form.Parts, static part => part.Text == "\"Bienvenue\"" && part.Slot == 0);
        Assert.Contains(form.Parts, static part => part.Text == "\"Error\"" && part.Slot == 1);
    }

    private static ErrorSignature Form(string message, string? frame = null, IReadOnlyList<ErrorRule>? rules = null) =>
        ErrorForms.Read(ErrorText.ScannedLines(message), frame, rules ?? []).Form;
}
