# Product source

## All code

- `AdoToolkit.Core` never references `System.Management.Automation`. Runtime dependencies
  are BCL-only (DD-011); the PowerShell package is a compile-time reference only.
- One public type per file.
- No static session state: connections and caches belong to their runspace
  (`SessionStateRegistry`). A static cache of type metadata is not session state.
  `UpdateAdoToolkitCommand.TestTransport` is the one mutable static member: a test seam that
  is null in production and that only the Pester tests set.
- Human text takes an explicit `CultureInfo`. Machine formats use the invariant culture.
  Identifiers compare ordinally.
- Warnings are errors. Fix the cause; do not suppress.

## Strings

- `src/AdoToolkit.Core/Resources/Strings.resx` (English) and
  `src/AdoToolkit.Core/Resources/Strings.fr.resx` (French) are the only catalog.
  Every key is an `AdoMessage` member or a `DiagnosticCodes` constant, in both files,
  with the same placeholders. `ResourceParityTests` enforces this.
- Read a string with `Messages.Get(AdoMessage.<Key>, culture, args)`. No string literal inside
  `WriteWarning`, `WriteVerbose`, `WriteDebug`, `WriteInformation`, `ProgressRecord`,
  `ErrorDetails` or an `Ado*Exception` constructor (`HardCodedStringTests`).
- French: `build` is masculine, a test run is « série de tests », and a no-break space
  (U+00A0) precedes `:`. `FrenchTerminologyTests` checks all three in the catalog and the
  help sources.
- ADO content is never translated or re-cased.

## Cmdlets (`src/AdoToolkit.PowerShell/Commands/`)

- One file per cmdlet, derived from `AdoCmdletBase`. Run the body in `RunLocal`; run
  requests in `RunWorker`.
- Throw an `Ado*Exception`. `ErrorRecordFactory` derives the error ID and category from the
  type; only configuration, authentication and authorization errors terminate.
- Typed pipeline input: call `EnsureInput(input, input.CollectionUri, connection)` before
  using it. It reports a hand-made incomplete object and an object from another collection.
- `-Name` wildcard filters use `NameFilter` and declare `[SupportsWildcards]`.
- Parameter help follows `docs/commands/AGENTS.md`.

## Core

- Every request goes through `AdoHttpPipeline` with an `EndpointRegistry` entry.
  WorkItemsBatch reads go through `WorkItemBatchReader`.
- Report text is encoded at the sink (`SinkEncoding`, `ContentLinks`). Remote text never
  enters a script element, a path or a URL; links are built from the connection and IDs.
- Files are written through `AtomicFileWriter`, `AtomicFileReplace` (the configuration
  file) or `GenerationFolderCommit`.
- `src/AdoToolkit.Core/Update/`, the engine of `Update-AdoToolkit`, is the one exception to
  the three rules above. Its own `HttpClient` calls GitHub outside `AdoHttpPipeline` and follows
  redirects only to the three hosts that `UpdateHttp` names; `UpdateFolderCommit` writes the
  new version; the checked entry names of the portable runtime become paths. It uses nothing
  of `Http/`, `Connections/` or `Configuration/`, and only the cmdlet and `ModuleManifestReader`
  use it (`UpdateIsolationTests`).
