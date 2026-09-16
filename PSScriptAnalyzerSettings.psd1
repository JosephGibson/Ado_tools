# PSScriptAnalyzer configuration for `tools\dev.ps1 verify` and editor integration.
# The gate keeps correctness and compatibility findings. Formatting remains an editor
# concern so a pre-existing style baseline cannot hide actionable diagnostics.
@{
    Severity     = @('Error', 'Warning')

    ExcludeRules = @(
        # Console progress output is intentional in these scripts.
        'PSAvoidUsingWriteHost',
        # Hook helpers and optional check scripts are single-file tools, not shipped modules.
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
            # Product runtime compatibility belongs in a product-specific analyzer
            # configuration and tools/check.ps1, not in the tooling's global rules.
            TargetVersions = @('7.0')
        }
    }
}
