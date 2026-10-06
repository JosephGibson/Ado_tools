namespace AdoToolkit.Core.Reporting.Errors;

// Text of an error as the server sent it, or one slot of it: Slot is -1 for literal text. Forms of
// one layout number their slots alike, so the values of a slot line up across them.
internal sealed record ErrorPart(string Text, int Slot);
