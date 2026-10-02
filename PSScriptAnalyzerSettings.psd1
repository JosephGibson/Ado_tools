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
        # Formatting is left to .editorconfig and the editor rather than this gate.
        'PSPlaceOpenBrace',
        'PSPlaceCloseBrace',
        'PSUseConsistentIndentation',
        # The functions are helpers of scripts and tests, not cmdlets of a module: a plural noun
        # is allowed, and a New-, Set- or Remove- helper takes no -WhatIf.
        'PSUseSingularNouns',
        'PSUseShouldProcessForStateChangingFunctions',
        # The rule does not see a parameter that another script block reads (a mock, a function
        # of the same script), and a Pester test case declares every value of its row.
        'PSReviewUnusedParameter',
        # Files are UTF-8 without a byte order mark, as .editorconfig sets.
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
