# PSScriptAnalyzer configuration for `tools\dev.ps1 verify` and editor integration.
# The gate keeps correctness and compatibility findings. Formatting remains an editor
# concern so a pre-existing style baseline cannot hide actionable diagnostics.
@{
    Severity     = @('Error', 'Warning')

    ExcludeRules = @(
        # Console progress output is intentional in these scripts.
        'PSAvoidUsingWriteHost',
        # The scripts are single-file tools and dot-sourced libraries, not modules: a variable
        # assigned in one of them is often read by another.
        'PSUseDeclaredVarsMoreThanAssignments',
        # Formatting is enforced by .editorconfig/editor tooling rather than this gate.
        'PSPlaceOpenBrace',
        'PSPlaceCloseBrace',
        'PSUseConsistentIndentation',
        'PSUseSingularNouns',
        'PSReviewUnusedParameter',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSUseBOMForUnicodeEncodedFile'
    )

    Rules        = @{
        PSUseCompatibleSyntax = @{
            Enable         = $true
            # A syntax floor for the 7.x line. The scripts themselves require PowerShell
            # 7.6.5 or later; tools/dev.ps1 checks that when it starts.
            TargetVersions = @('7.0')
        }
    }
}
