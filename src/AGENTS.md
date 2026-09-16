# Product source

- C# 14, .NET 10; the binary module targets PowerShell 7.6 on Windows.
- Core never references `System.Management.Automation`. Runtime dependencies are
  BCL-only (DD-011); the PowerShell package is a compile-time reference only.
- Human text takes an explicit `CultureInfo`; machine formats use invariant
  culture and identifiers use ordinal comparisons.
- All user-facing strings come from string-only neutral English and French `.resx`
  resources. Keep one public type per file.
- No static session state; connections and caches belong to their runspace.
