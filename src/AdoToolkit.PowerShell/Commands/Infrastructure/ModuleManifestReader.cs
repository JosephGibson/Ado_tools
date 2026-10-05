using System.Collections;
using System.Management.Automation.Language;
using AdoToolkit.Core.Update;

namespace AdoToolkit.Commands.Infrastructure;

// Reads a module manifest as Import-PowerShellDataFile does, which is what Install-AdoToolkit.ps1
// calls: the file must parse, and its first hashtable must hold constants only. Null otherwise.
internal static class ModuleManifestReader
{
    internal static ModuleManifestFacts? Read(string path)
    {
        ScriptBlockAst ast = Parser.ParseFile(path, out _, out ParseError[] errors);
        if (errors.Length > 0 || ast.Find(static node => node is HashtableAst, searchNestedScriptBlocks: false) is not HashtableAst data) return null;
        object value;
        try { value = data.SafeGetValue(); }
        catch (InvalidOperationException) { return null; }
        if (value is not Hashtable table) return null;
        return new ModuleManifestFacts
        {
            ModuleVersion = table["ModuleVersion"] as string,
            RootModule = table["RootModule"] as string,
            PowerShellVersion = table["PowerShellVersion"] as string,
        };
    }
}
