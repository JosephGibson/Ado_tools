# Cmdlet help sources

`docs/commands/en-US/` and `docs/commands/fr-CA/` each hold one PlatyPS Markdown topic per
cmdlet. `tools/package/Publish-AdoToolkitPackage.ps1` compiles them into the package as
`en-US/` and `fr/AdoToolkit.PowerShell.dll-Help.xml`. They are shipped, end-user content:
keep the explanation.

## Rules

- Change both cultures in the same edit. Section headings, `SYNTAX`, every `yaml` block,
  example code and `RELATED LINKS` are identical in both files; only prose differs, and
  the French prose says what the English prose says.
- Section headings (`## SYNOPSIS`, `## PARAMETERS`, …) stay in English: PlatyPS parses them.
  French examples are headed `### Exemple N`.
- A parameter's `yaml` block matches the compiled cmdlet: `Type` as the reflection name
  (``System.Nullable`1[System.Int32]``), `Aliases` (`wi` and `cf` for `-WhatIf` and
  `-Confirm`), `SupportsWildcards`, and one `ParameterSets` entry per set.
- Every topic has a synopsis, a description, at least one example and a description for
  every parameter. The text under an example describes what its code does.
- Put a blank line before and after each heading and each code fence.
- Set `ms.date` (`MM-dd-yyyy`) to the day of the change in each file you change.
- French terms follow the Strings section of `src/AGENTS.md`. In help, a non-terminating
  error is « erreur non bloquante », the platform is « sous Windows », a pipeline stage is
  « phase », and no space precedes `;`.

## Checks

| Test | Proves |
| --- | --- |
| `tests/AdoToolkit.PowerShell.Tests/HelpMetadata.Pester.ps1` | Parameter names, types, aliases, wildcard support and set binding equal the compiled cmdlet, in both cultures |
| `tests/AdoToolkit.PowerShell.Tests/Help.Pester.ps1` | The compiled help loads under `en-US`, `fr-CA`, `fr-FR` and `fr`, complete for every cmdlet |
| `tools/package/Publish-AdoToolkitPackage.ps1` | The sources of each culture compile to one MAML file that holds every command |
