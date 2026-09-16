function Invoke-ToolBootstrap {
    param(
        [string] $Root = $script:RepositoryRoot,
        [switch] $Install
    )

    $diagnostics = Get-ProjectDiagnostics -Root $Root
    $actions = New-Object System.Collections.ArrayList
    if ($Install) {
        foreach ($tool in $diagnostics.Tools | Where-Object { $_.State -ne 'present' -and $_.Level -ne 'optional' }) {
            try {
                $installSupported = $true
                switch ($tool.Name) {
                    'ripgrep' {
                        if (-not (Get-FirstCommand -Names @('winget'))) { throw 'winget is unavailable.' }
                        $installOutput = @(& winget install --id BurntSushi.ripgrep.MSVC --exact --source winget --accept-package-agreements --accept-source-agreements --silent 2>&1)
                        if ($LASTEXITCODE -ne 0) { throw ($installOutput | Select-Object -Last 1) }
                    }
                    'Pester' {
                        $previousWarningPreference = $WarningPreference
                        try {
                            $WarningPreference = 'SilentlyContinue'
                            Install-Module -Name Pester -MinimumVersion 5.0 -MaximumVersion 5.999.999 -Scope CurrentUser -Repository PSGallery -Force -AllowClobber -Confirm:$false
                        }
                        finally { $WarningPreference = $previousWarningPreference }
                    }
                    'PSScriptAnalyzer' {
                        $previousWarningPreference = $WarningPreference
                        try {
                            $WarningPreference = 'SilentlyContinue'
                            Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Repository PSGallery -Force -AllowClobber -Confirm:$false
                        }
                        finally { $WarningPreference = $previousWarningPreference }
                    }
                    default { $installSupported = $false }
                }
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
        Notes = @('Install is explicit. ast-grep is intentionally optional and is not installed automatically.')
    }
}

function Set-AtomicText {
    param([string] $Path, [string] $Content)

    $temporary = Join-Path (Split-Path -Parent $Path) ('.' + [System.IO.Path]::GetFileName($Path) + '.' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllText($temporary, $Content.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))
        if ([System.IO.File]::ReadAllText($temporary) -cne $Content.Replace("`r`n", "`n")) { throw 'Temporary output validation failed.' }
        [System.IO.File]::Move($temporary, $Path, $true)
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
}

function Initialize-Project {
    param([string] $Root = $script:RepositoryRoot, [string] $ProjectName, [string] $Description)

    if ($ProjectName -notmatch '^[A-Za-z0-9][A-Za-z0-9 ._-]{0,79}$') { throw 'init requires -ProjectName (1-80 letters, digits, spaces, dots, underscores or hyphens).' }
    if ([string]::IsNullOrWhiteSpace($Description) -or $Description.Length -gt 500 -or $Description -match '[\r\n\x00-\x1F]') { throw 'init requires a single-line -Description of at most 500 characters.' }
    $escapedDescription = [regex]::Replace($Description.Trim(), '[\\`*_{}\[\]()<>|#]', '\$0')
    $replacement = "<!-- project:start -->`n# $($ProjectName.Trim())`n`n$escapedDescription`n<!-- project:end -->"
    $originals = [ordered]@{}
    $updates = [ordered]@{}
    foreach ($name in @('README.md', 'AGENTS.md')) {
        $path = [System.IO.Path]::GetFullPath((Join-Path $Root $name))
        $file = Get-Item -LiteralPath $path -Force
        if (-not (Test-IsSafeRepositoryFile -File $file -Root $Root)) { throw "$name must be a regular repository file." }
        $content = [System.IO.File]::ReadAllText($path)
        $pattern = '(?s)<!-- project:start -->.*?<!-- project:end -->'
        if ([regex]::Matches($content, $pattern).Count -ne 1) { throw "$name must contain exactly one project marker pair. No files changed." }
        $originals[$path] = $content
        $updated = [regex]::Replace($content, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $replacement })
        if ($updated -cne $content) { $updates[$path] = $updated }
    }
    $written = [System.Collections.Generic.List[string]]::new()
    try {
        foreach ($path in $updates.Keys) { Set-AtomicText -Path $path -Content $updates[$path]; $written.Add($path) }
    }
    catch {
        foreach ($path in $written) { Set-AtomicText -Path $path -Content $originals[$path] }
        throw
    }
    return [pscustomobject]@{ Status = 'ok'; Project = $ProjectName.Trim(); Changed = @($written | ForEach-Object { Get-RelativeRepositoryPath -Path $_ -Root $Root }); Next = 'Add product code/manifests, then run dev.ps1 plan and verify.' }
}
