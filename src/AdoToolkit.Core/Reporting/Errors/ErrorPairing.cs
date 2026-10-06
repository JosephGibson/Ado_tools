namespace AdoToolkit.Core.Reporting.Errors;

// Why two forms are one error: Test failed with them at the same place in an English group and a
// French group of the build.
internal sealed record ErrorPairing(int Test, int EnglishGroup, int FrenchGroup);
