namespace AdoToolkit.Core.IO;

// §13.4 step 1: everything a render needs to link to the final folder before any download.
internal sealed record GenerationFolderPlan(string ReportPath, string Directory, string BaseName, string Stamp, string FolderName)
{
    internal string FolderPath => Path.Combine(Directory, FolderName);
}
