function Invoke-ToolBootstrap {
    param(
        [string] $Root = $script:RepositoryRoot,
        [switch] $Install
    )

    $diagnostics = Get-ProjectDiagnostics -Root $Root
    $actions = New-Object System.Collections.ArrayList
    $fromWinget = New-Object System.Collections.ArrayList
    if ($Install) {
        # Build modules install for the current user from PSGallery, within the major version
        # verify accepts. The .NET SDK has no supported unattended install here.
        $moduleRanges = @{
            'Pester' = @{ MinimumVersion = '5.0'; MaximumVersion = '5.999.999' }
            'PSScriptAnalyzer' = @{}
            'Microsoft.PowerShell.PlatyPS' = @{ MinimumVersion = '1.0'; MaximumVersion = '1.999.999' }
        }
        # Command-line tools install with winget, by their identifier in its community source.
        $wingetPackages = @{
            'ripgrep' = 'BurntSushi.ripgrep.MSVC'
            'actionlint' = 'rhysd.actionlint'
        }
        foreach ($tool in $diagnostics.Tools | Where-Object { $_.State -ne 'present' }) {
            try {
                $installSupported = $true
                if ($wingetPackages.ContainsKey($tool.Name)) {
                    if (-not (Get-FirstCommand -Names @('winget'))) { throw 'winget is unavailable.' }
                    $installOutput = @(& winget install --id $wingetPackages[$tool.Name] --exact --source winget --accept-package-agreements --accept-source-agreements --silent 2>&1)
                    if ($LASTEXITCODE -ne 0) { throw ($installOutput | Select-Object -Last 1) }
                    [void] $fromWinget.Add($tool.Name)
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
    $notes = @('Nothing is installed without -Install.')
    # winget may extend PATH for new processes only; this one keeps the PATH it started with.
    if (@($currentDiagnostics.Tools | Where-Object { $_.Name -in $fromWinget -and $_.State -eq 'missing' }).Count -gt 0) {
        $notes += 'A tool that winget installed is still missing here: it is found from a new terminal.'
    }
    return [pscustomobject]@{
        Status = if (@($actions | Where-Object { $_.Status -eq 'failed' }).Count -gt 0) { 'incomplete' } else { $currentDiagnostics.Status }
        Tools = $currentDiagnostics.Tools
        Actions = @($actions)
        Install = [bool] $Install
        Notes = @($notes)
    }
}
