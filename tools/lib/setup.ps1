function Invoke-ToolBootstrap {
    param(
        [string] $Root = $script:RepositoryRoot,
        [switch] $Install
    )

    $diagnostics = Get-ProjectDiagnostics -Root $Root
    $actions = New-Object System.Collections.ArrayList
    if ($Install) {
        # Build modules install for the current user from PSGallery, within the major version
        # verify accepts. The .NET SDK has no supported unattended install here.
        $moduleRanges = @{
            'Pester' = @{ MinimumVersion = '5.0'; MaximumVersion = '5.999.999' }
            'PSScriptAnalyzer' = @{}
            'Microsoft.PowerShell.PlatyPS' = @{ MinimumVersion = '1.0'; MaximumVersion = '1.999.999' }
        }
        foreach ($tool in $diagnostics.Tools | Where-Object { $_.State -ne 'present' }) {
            try {
                $installSupported = $true
                if ($tool.Name -eq 'ripgrep') {
                    if (-not (Get-FirstCommand -Names @('winget'))) { throw 'winget is unavailable.' }
                    $installOutput = @(& winget install --id BurntSushi.ripgrep.MSVC --exact --source winget --accept-package-agreements --accept-source-agreements --silent 2>&1)
                    if ($LASTEXITCODE -ne 0) { throw ($installOutput | Select-Object -Last 1) }
                }
                elseif ($moduleRanges.ContainsKey($tool.Name)) {
                    $range = $moduleRanges[$tool.Name]
                    $previousWarningPreference = $WarningPreference
                    try {
                        $WarningPreference = 'SilentlyContinue'
                        Install-Module -Name $tool.Name @range -Scope CurrentUser -Repository PSGallery -Force -AllowClobber -Confirm:$false
                    }
                    finally { $WarningPreference = $previousWarningPreference }
                }
                else { $installSupported = $false }
                if ($installSupported) {
                    [void] $actions.Add([pscustomobject]@{ Name = $tool.Name; Status = 'installed' })
                }
                else {
                    [void] $actions.Add([pscustomobject]@{ Name = $tool.Name; Status = 'unsupported' })
                }
            }
            catch {
                [void] $actions.Add([pscustomobject]@{ Name = $tool.Name; Status = 'failed'; Detail = $_.Exception.Message })
            }
        }
    }

    $currentDiagnostics = if ($Install) { Get-ProjectDiagnostics -Root $Root } else { $diagnostics }
    return [pscustomobject]@{
        Status = if (@($actions | Where-Object { $_.Status -eq 'failed' }).Count -gt 0) { 'incomplete' } else { $currentDiagnostics.Status }
        Tools = $currentDiagnostics.Tools
        Actions = @($actions)
        Install = [bool] $Install
        Notes = @('Nothing is installed without -Install.')
    }
}
