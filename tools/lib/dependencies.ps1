function Get-DependencyInventory {
    param(
        [string] $Root = $script:RepositoryRoot,
        [ValidateRange(1, 100)][int] $Limit = 20
    )

    $projectProfile = Get-ProjectProfile -Root $Root
    $Root = $projectProfile.Root
    $packages = New-Object System.Collections.ArrayList
    $manifests = New-Object System.Collections.ArrayList
    $errors = New-Object System.Collections.ArrayList

    # Package references come from project files and central props/targets; nuget.config owns feeds.
    foreach ($file in $projectProfile.Files | Where-Object { $_.Extension -in @('.csproj', '.props', '.targets') }) {
        $relative = Get-RelativeRepositoryPath -Path $file.FullName -Root $Root
        [void] $manifests.Add($relative)
        try {
            $project = Get-SafeXmlDocument -Path $file.FullName
            foreach ($reference in @($project.SelectNodes('//*[local-name()="PackageReference" or local-name()="PackageVersion"]'))) {
                # GetAttribute rather than property access: a reference that omits
                # Version — the normal shape under central package management — would
                # otherwise throw under Set-StrictMode and lose the whole manifest.
                $name = [string] $reference.GetAttribute('Include')
                if ([string]::IsNullOrWhiteSpace($name)) { $name = [string] $reference.GetAttribute('Update') }
                if ([string]::IsNullOrWhiteSpace($name)) { continue }

                $version = [string] $reference.GetAttribute('Version')
                if ([string]::IsNullOrWhiteSpace($version)) {
                    $versionNode = $reference.SelectSingleNode('./*[local-name()="Version"]')
                    if ($null -ne $versionNode) { $version = [string] $versionNode.InnerText }
                }
                if ([string]::IsNullOrWhiteSpace($version)) { $version = $null }
                $scope = if ($reference.LocalName -eq 'PackageVersion') { 'central' } else { 'project' }
                [void] $packages.Add([pscustomobject]@{ Manager = 'NuGet'; Name = $name; Version = $version; Scope = $scope; Manifest = $relative })
            }
        }
        catch {
            [void] $errors.Add("${relative}: $($_.Exception.Message)")
        }
    }

    $validationTools = @((Get-RequiredTool -ProjectProfile $projectProfile).Name | Where-Object { $_ -ne 'PowerShell' })

    $allPackages = @($packages | Sort-Object Manager, Name, Manifest)
    return [pscustomobject]@{
        Status = if ($errors.Count -gt 0) { 'incomplete' } else { 'ok' }
        Manifests = @($manifests | Sort-Object -Unique)
        Packages = @($allPackages | Select-Object -First $Limit)
        PackageCount = $allPackages.Count
        Truncated = $allPackages.Count -gt $Limit
        Errors = @($errors | Select-Object -First $Limit)
        ValidationTools = @($validationTools | Sort-Object -Unique)
    }
}

function Get-BuildModule {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Pester', 'PSScriptAnalyzer', 'Microsoft.PowerShell.PlatyPS')][string] $Name,
        [switch] $Loaded
    )

    # Release children inherit this flag. Installation and every import use the same
    # manifest, so a newer module preinstalled on the runner cannot change the build.
    $pinned = $null
    if ($env:ADOTOOLKIT_RELEASE_BUILD -eq '1') {
        $versions = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot '../BuildModules.psd1')
        $pinned = [version] $versions[$Name]
    }
    Get-Module -Name $Name -ListAvailable:(-not $Loaded) | Where-Object {
        if ($null -ne $pinned) { $_.Version -eq $pinned }
        elseif ($Name -eq 'Pester') { $_.Version.Major -eq 5 }
        elseif ($Name -eq 'Microsoft.PowerShell.PlatyPS') { $_.Version.Major -eq 1 }
        else { $true }
    } | Sort-Object Version -Descending | Select-Object -First 1
}

function Get-ToolState {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [AllowEmptyCollection()][string[]] $Commands = @(),
        [string] $MinimumVersion,
        [string] $MaximumVersion,
        [ValidateSet('required', 'recommended')][string] $Level = 'recommended',
        [switch] $Module
    )

    if ($Module) {
        $availableModules = @(Get-Module -ListAvailable -Name $Name | Sort-Object Version -Descending)
        $compatibleModules = @(
            $availableModules | Where-Object {
                ([string]::IsNullOrWhiteSpace($MinimumVersion) -or $_.Version -ge [version] $MinimumVersion) -and
                ([string]::IsNullOrWhiteSpace($MaximumVersion) -or $_.Version -lt [version] $MaximumVersion)
            }
        )
        if ($compatibleModules.Count -gt 0) { $found = $compatibleModules[0] }
        elseif ($availableModules.Count -gt 0) { $found = $availableModules[0] }
        else { $found = $null }
        $version = if ($null -ne $found) { $found.Version } else { $null }
        $present = $null -ne $found
    }
    else {
        $found = Get-FirstCommand -Names $Commands
        $versionInfo = if ($null -ne $found) { Get-CommandVersion -Names $Commands } else { $null }
        $versionText = if ($null -ne $versionInfo) { $versionInfo.Version } else { $null }
        $version = $null
        if ($versionText -match '(\d+(?:\.\d+)+)') {
            try { $version = [version] $matches[1] } catch { $version = $null }
        }
        $present = $null -ne $found
    }

    $state = if (-not $present) { 'missing' } elseif ($MinimumVersion -and $version -and $version -lt [version] $MinimumVersion) { 'outdated' } elseif ($MaximumVersion -and $version -and $version -ge [version] $MaximumVersion) { 'incompatible' } else { 'present' }
    return [pscustomobject]@{
        Name = $Name
        Level = $Level
        State = $state
        Version = if ($null -ne $version) { $version.ToString() } else { $null }
        MinimumVersion = if ([string]::IsNullOrWhiteSpace($MinimumVersion)) { $null } else { $MinimumVersion }
        MaximumVersion = if ([string]::IsNullOrWhiteSpace($MaximumVersion)) { $null } else { $MaximumVersion }
    }
}

# The tools verify needs for this repository's content. One list feeds diagnose, deps and bootstrap.
function Get-RequiredTool {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    [pscustomobject]@{ Name = 'PowerShell'; Module = $false; Commands = @('pwsh'); MinimumVersion = '7.6.5'; MaximumVersion = $null }
    if ($ProjectProfile.Stack -contains 'powershell') {
        if (@($ProjectProfile.TestFiles | Where-Object { $_ -match '\.(?:Tests|Pester)\.ps1$' }).Count -gt 0) {
            [pscustomobject]@{ Name = 'Pester'; Module = $true; Commands = @(); MinimumVersion = '5.0'; MaximumVersion = '6.0' }
        }
        [pscustomobject]@{ Name = 'PSScriptAnalyzer'; Module = $true; Commands = @(); MinimumVersion = $null; MaximumVersion = $null }
    }
    if ($ProjectProfile.Stack -contains 'dotnet') {
        [pscustomobject]@{ Name = 'dotnet'; Module = $false; Commands = @('dotnet'); MinimumVersion = $null; MaximumVersion = $null }
    }
    # Command help is compiled from docs/commands at packaging time.
    if (@($ProjectProfile.Files | Where-Object { $_.Extension -eq '.md' -and
                (Get-RelativeRepositoryPath -Path $_.FullName -Root $ProjectProfile.Root) -like 'docs/commands/*' }).Count -gt 0) {
        [pscustomobject]@{ Name = 'Microsoft.PowerShell.PlatyPS'; Module = $true; Commands = @(); MinimumVersion = '1.0'; MaximumVersion = '2.0' }
    }
    # The workflow-lint stage runs it on the GitHub workflow files.
    if (@(Get-WorkflowFile -ProjectProfile $ProjectProfile).Count -gt 0) {
        [pscustomobject]@{ Name = 'actionlint'; Module = $false; Commands = @('actionlint'); MinimumVersion = $null; MaximumVersion = $null }
    }
}

function Get-ProjectDiagnostics {
    param([string] $Root = $script:RepositoryRoot)

    $projectProfile = Get-ProjectProfile -Root $Root
    $tools = New-Object System.Collections.ArrayList
    foreach ($tool in Get-RequiredTool -ProjectProfile $projectProfile) {
        $state = if ($tool.Module) { Get-ToolState -Name $tool.Name -MinimumVersion $tool.MinimumVersion -MaximumVersion $tool.MaximumVersion -Level 'required' -Module }
        else { Get-ToolState -Name $tool.Name -Commands $tool.Commands -MinimumVersion $tool.MinimumVersion -Level 'required' }
        [void] $tools.Add($state)
    }
    [void] $tools.Add((Get-ToolState -Name 'ripgrep' -Commands @('rg') -Level 'recommended'))

    $requiredProblems = @($tools | Where-Object { $_.Level -eq 'required' -and $_.State -ne 'present' })
    return [pscustomobject]@{
        Status = if ($requiredProblems.Count -eq 0) { 'ok' } else { 'incomplete' }
        PowerShell = $PSVersionTable.PSVersion.ToString()
        Stack = $projectProfile.Stack
        Tools = @($tools)
        RequiredProblems = @($requiredProblems | ForEach-Object { $_.Name })
    }
}
