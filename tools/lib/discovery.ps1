function Get-RelativeRepositoryPath {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Root
    )

    return [System.IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

function Test-IsAgentSafePath {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Root
    )

    $relativePath = Get-RelativeRepositoryPath -Path $Path -Root $Root
    return $relativePath -notmatch $script:SensitivePathPattern
}

function Test-IsRepositoryPath {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Root
    )

    try {
        $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $candidatePath = [System.IO.Path]::GetFullPath($Path)
        if ([string]::Equals($candidatePath, $rootPath, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }

        $rootPrefix = $rootPath + [System.IO.Path]::DirectorySeparatorChar
        return $candidatePath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        return $false
    }
}

function Test-IsSafeRepositoryFile {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo] $File,
        [Parameter(Mandatory = $true)][string] $Root
    )

    # Never follow file links while discovering a repository. A link can point outside
    # the workspace, where the secret-path exclusions would not protect it.
    if (($File.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
    if (-not (Test-IsRepositoryPath -Path $File.FullName -Root $Root) -or -not (Test-IsAgentSafePath -Path $File.FullName -Root $Root)) { return $false }
    $directory = $File.Directory
    while ($null -ne $directory -and -not [string]::Equals($directory.FullName, $Root.TrimEnd('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)) {
        if (($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or (Test-IsExcludedDirectoryName -Name $directory.Name)) { return $false }
        $directory = $directory.Parent
    }
    return $true
}

# Check each component before descending, including directory targets used by documentation.
function Test-IsSafeRepositoryTarget {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Root)

    try {
        $current = [System.IO.Path]::GetFullPath($Root)
        $target = [System.IO.Path]::GetFullPath($Path)
        if (-not (Test-IsRepositoryPath -Path $target -Root $current) -or
            -not (Test-IsAgentSafePath -Path $target -Root $current)) { return $false }
        $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
        $relative = (Get-RelativeRepositoryPath -Path $target -Root $current).TrimEnd('/')
        if ($relative -eq '.') { return $true }
        foreach ($part in $relative.Split('/')) {
            $current = Join-Path $current $part
            $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
            if ($item -is [System.IO.DirectoryInfo] -and (Test-IsExcludedDirectoryName -Name $part)) { return $false }
        }
        return $true
    }
    catch { return $false }
}

function Test-IsExcludedDirectoryName {
    param([Parameter(Mandatory = $true)][string] $Name)

    if ($Name -in $script:ExcludedDirectoryNames -or $Name -in $script:SensitiveDirectoryNames) { return $true }
    # ripgrep applies a file glob to a directory name too and does not descend into a match,
    # so the walk prunes the same directories.
    foreach ($glob in $script:SensitiveFileGlobs) {
        if ($Name -like $glob) { return $true }
    }
    return $false
}

function Test-IsExcludedRepositoryDirectory {
    param([Parameter(Mandatory = $true)][System.IO.DirectoryInfo] $Directory)

    return Test-IsExcludedDirectoryName -Name $Directory.Name
}

function Get-RepositoryExclusionGlobs {
    $globs = New-Object System.Collections.ArrayList
    foreach ($name in @($script:ExcludedDirectoryNames) + @($script:SensitiveDirectoryNames)) {
        [void] $globs.Add("!$name/**")
        [void] $globs.Add("!**/$name/**")
    }
    foreach ($glob in $script:SensitiveFileGlobs) {
        [void] $globs.Add("!$glob")
        [void] $globs.Add("!**/$glob")
    }

    return @($globs)
}

function Get-RepositorySearchGlobs {
    # --no-ignore keeps ripgrep from applying .gitignore on top of these globs. The
    # filesystem fallback cannot read .gitignore, and a discovery command whose answer
    # depends on which tool ran is worse than one that occasionally lists a build file.
    return @('--no-config', '--no-ignore') + @(Get-RepositoryExclusionGlobs | ForEach-Object { @('--iglob', $_) })
}

function Get-RepositoryFiles {
    param([Parameter(Mandatory = $true)][string] $Root)

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Repository root does not exist: $Root"
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $ripgrep = Get-FirstCommand -Names @('rg')
    if ($null -ne $ripgrep) {
        $globArguments = Get-RepositorySearchGlobs
        $paths = @(& $ripgrep.Source '--files' '--hidden' '--no-messages' @globArguments '--' $resolvedRoot 2>$null)
        if ($LASTEXITCODE -le 1) {
            $files = New-Object System.Collections.ArrayList
            foreach ($path in $paths) {
                $candidatePath = [string] $path
                if (-not [System.IO.Path]::IsPathRooted($candidatePath)) {
                    $candidatePath = Join-Path $resolvedRoot $candidatePath
                }
                try {
                    $item = Get-Item -LiteralPath $candidatePath -Force -ErrorAction Stop
                    if ($item -is [System.IO.FileInfo] -and (Test-IsSafeRepositoryFile -File $item -Root $resolvedRoot)) {
                        [void] $files.Add($item)
                    }
                }
                catch { continue }
            }
            return @($files | Sort-Object FullName)
        }
    }

    $files = New-Object System.Collections.ArrayList
    $directories = New-Object 'System.Collections.Generic.Queue[string]'
    $directories.Enqueue($resolvedRoot)
    while ($directories.Count -gt 0) {
        $currentDirectory = $directories.Dequeue()
        $children = @(Get-ChildItem -LiteralPath $currentDirectory -Force -ErrorAction SilentlyContinue)
        foreach ($child in $children) {
            if ($child -is [System.IO.DirectoryInfo]) {
                $isLink = ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
                if (-not $isLink -and -not (Test-IsExcludedRepositoryDirectory -Directory $child)) { $directories.Enqueue($child.FullName) }
            }
            elseif ($child -is [System.IO.FileInfo] -and (Test-IsSafeRepositoryFile -File $child -Root $resolvedRoot)) {
                [void] $files.Add($child)
            }
        }
    }

    return @($files | Sort-Object FullName)
}

function Get-FirstCommand {
    param([Parameter(Mandatory = $true)][string[]] $Names)

    foreach ($name in $Names) {
        $command = Get-Command -Name $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $command) { return $command }
    }

    return $null
}

function Get-CommandVersion {
    param([Parameter(Mandatory = $true)][string[]] $Names)

    $command = Get-FirstCommand -Names $Names
    if ($null -eq $command) { return $null }

    try {
        $version = @(& $command.Source --version 2>$null | Select-Object -First 1)
        return [pscustomobject]@{
            Name = $command.Name
            Version = if ($version.Count -gt 0) { [string] $version[0] } else { $null }
        }
    }
    catch {
        return [pscustomobject]@{ Name = $command.Name; Version = $null }
    }
}

function ConvertFrom-StrictJson {
    param([AllowEmptyString()][string] $Text)
    $document = [System.Text.Json.JsonDocument]::Parse($Text)
    try { return ConvertFrom-Json -InputObject $Text -NoEnumerate }
    finally { $document.Dispose() }
}

function Get-SafeXmlDocument {
    param([Parameter(Mandatory = $true)][string] $Path)

    $reader = $null
    try {
        $settings = New-Object System.Xml.XmlReaderSettings
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [System.Xml.XmlReader]::Create($Path, $settings)
        $document = New-Object System.Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
    }
}

function Get-ProjectProfile {
    param([string] $Root = $script:RepositoryRoot)

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Repository root does not exist: $Root"
    }
    $Root = (Resolve-Path -LiteralPath $Root).Path
    $files = @(Get-RepositoryFiles -Root $Root)
    $relative = @{}
    # A name set rather than $files.Name: member enumeration over an empty array is an
    # error under Set-StrictMode 2.0, which would make every command throw on a
    # repository that has nothing in it yet.
    $fileNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $files) {
        $relative[$file.FullName] = Get-RelativeRepositoryPath -Path $file.FullName -Root $Root
        [void] $fileNames.Add($file.Name)
    }

    # The product is a .NET solution with PowerShell tooling and tests; nothing else is detected.
    $stacks = New-Object System.Collections.ArrayList
    $languages = New-Object System.Collections.ArrayList
    $solutionFiles = @($files | Where-Object { $_.Extension -in @('.sln', '.slnx') })
    $dotnetProjects = @($files | Where-Object { $_.Extension -eq '.csproj' })
    if ($solutionFiles.Count -gt 0 -or $dotnetProjects.Count -gt 0 -or $fileNames.Contains('global.json')) {
        [void] $stacks.Add('dotnet')
        if ($dotnetProjects.Count -gt 0) { [void] $languages.Add('C#') }
    }
    $projectFiles = @(@($solutionFiles) + @($dotnetProjects) | ForEach-Object { $relative[$_.FullName] })
    if (@($files | Where-Object { $_.Extension -in @('.ps1', '.psm1', '.psd1') }).Count -gt 0) {
        [void] $stacks.Add('powershell')
        [void] $languages.Add('PowerShell')
    }

    # Living under tests/ is not enough: the directory also holds fixtures, support scripts and
    # data files. A PowerShell test is named *.Tests.ps1 or *.Pester.ps1; a C# test is any .cs
    # file of a test project folder.
    $testFiles = @(
        $files | Where-Object {
            ($_.Extension -eq '.cs' -and $relative[$_.FullName] -match '(?:^|/)tests?/') -or
            $_.Name -match '(?i)\.(?:tests|pester)\.ps1$'
        }
    )

    $majorDirectories = @(
        Get-ChildItem -LiteralPath $Root -Force -Directory |
            Where-Object { -not (Test-IsExcludedRepositoryDirectory -Directory $_) -and ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0 } |
            Sort-Object Name | ForEach-Object { $_.Name }
    )
    $configuration = @(
        $files | Where-Object {
            $relativePath = $relative[$_.FullName]
            $relativePath -notmatch '^\.claude/' -and
            $_.Name -in @('AGENTS.md', 'CLAUDE.md', 'README.md', '.editorconfig', '.gitattributes', '.gitignore', '.mcp.json', 'global.json', 'nuget.config', 'Directory.Build.props', 'Directory.Packages.props', 'PSScriptAnalyzerSettings.psd1')
        } | ForEach-Object { $relative[$_.FullName] }
    )

    return [pscustomobject]@{
        Root = $Root
        Stack = @($stacks | Sort-Object -Unique)
        Languages = @($languages | Sort-Object -Unique)
        ProjectFiles = @($projectFiles | Sort-Object -Unique)
        TestFiles = @($testFiles | ForEach-Object { $relative[$_.FullName] })
        MajorDirectories = $majorDirectories
        Configuration = @($configuration | Sort-Object -Unique)
        FileCount = $files.Count
        Files = $files
    }
}

function Get-RecommendedProjectCommands {
    $devCommand = 'pwsh -NoProfile -File .\tools\dev.ps1'
    $commands = [ordered]@{
        Inspect = "$devCommand inspect"
        Context = "$devCommand context"
        Find = "$devCommand find -Query '<term>'"
        Verify = "$devCommand verify"
        Plan = "$devCommand plan"
        Diagnose = "$devCommand diagnose"
        Dependencies = "$devCommand deps"
        Bootstrap = "$devCommand bootstrap"
    }
    return [pscustomobject] $commands
}

function Select-UniquePath {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Path)

    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $Path) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and $seen.Add($candidate)) { $candidate }
    }
}

function Get-AgentInstructionPaths {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    # A nested AGENTS.md or CLAUDE.md is the documented way to scope rules to one
    # subtree, so match at any depth rather than only at the repository root.
    return @(
        foreach ($file in $ProjectProfile.Files) {
            $relativePath = Get-RelativeRepositoryPath -Path $file.FullName -Root $ProjectProfile.Root
            if ($relativePath -match '^(?:.+/)?(?:AGENTS(?:\.override)?|CLAUDE)\.md$' -or
                $relativePath -match '^\.claude/(?:settings\.json|rules/.+\.md)$') {
                $relativePath
            }
        }
    ) | Sort-Object -Unique
}

function Get-ProjectInspection {
    param([string] $Root = $script:RepositoryRoot)

    $projectProfile = Get-ProjectProfile -Root $Root
    return [pscustomobject]@{
        Status = 'ok'
        Root = $projectProfile.Root
        Stack = $projectProfile.Stack
        Languages = $projectProfile.Languages
        ProjectFiles = $projectProfile.ProjectFiles
        Tests = [pscustomobject]@{ Files = $projectProfile.TestFiles; Count = $projectProfile.TestFiles.Count }
        Directories = $projectProfile.MajorDirectories
        Configuration = $projectProfile.Configuration
        AgentInstructions = @(Get-AgentInstructionPaths -ProjectProfile $projectProfile)
        FileCount = $projectProfile.FileCount
        Commands = Get-RecommendedProjectCommands
    }
}

function Get-ProjectContext {
    param([string] $Root = $script:RepositoryRoot, [string] $Path)

    $projectProfile = Get-ProjectProfile -Root $Root
    $workflowPaths = @('tools/dev.ps1', 'tools/check.ps1', 'docs/tooling.md') |
        Where-Object { Test-Path -LiteralPath (Join-Path $projectProfile.Root $_) -PathType Leaf }
    # Kept in priority order, not sorted: this list has a budget, and truncating an
    # alphabetical list would drop the workflow and instruction files that matter most.
    # Test files are counted, not listed: find and the path-scoped rules locate them.
    $importantPaths = @(Select-UniquePath -Path @(
            @($workflowPaths)
            @(Get-AgentInstructionPaths -ProjectProfile $projectProfile)
            @($projectProfile.Configuration)
            @($projectProfile.ProjectFiles)
        ))
    $importantPathBudget = 40
    $scope = if ($Path) { Get-ScopedInstructions -ProjectProfile $projectProfile -Path $Path } else { @() }
    $projectName = Split-Path -Leaf $projectProfile.Root
    $contract = $projectProfile.Files | Where-Object { $_.FullName -eq (Join-Path $projectProfile.Root 'AGENTS.md') } | Select-Object -First 1
    if ($contract) {
        $heading = Get-Content -LiteralPath $contract.FullName -TotalCount 8 | Where-Object { $_ -match '^# .+' } | Select-Object -First 1
        if ($heading) { $projectName = $heading.Substring(2).Trim() }
    }
    return [pscustomobject]@{
        Status = 'ok'
        Project = $projectName
        Stack = $projectProfile.Stack
        Languages = $projectProfile.Languages
        Tests = $projectProfile.TestFiles.Count
        ProductTests = @($projectProfile.TestFiles | Where-Object { $_ -notlike 'tools/*' }).Count
        ScopedInstructions = @($scope)
        ImportantPaths = @($importantPaths | Select-Object -First $importantPathBudget)
        ImportantPathCount = $importantPaths.Count
        Commands = Get-RecommendedProjectCommands
    }
}

function Get-ScopedInstructions {
    param([object] $ProjectProfile, [string] $Path)

    $absolute = [System.IO.Path]::GetFullPath($Path, $ProjectProfile.Root)
    if (-not (Test-IsRepositoryPath -Path $absolute -Root $ProjectProfile.Root) -or
        -not (Test-IsAgentSafePath -Path $absolute -Root $ProjectProfile.Root)) { throw 'Context path must be a safe repository path.' }
    $relative = (Get-RelativeRepositoryPath -Path $absolute -Root $ProjectProfile.Root).TrimEnd('/')
    foreach ($instruction in Get-AgentInstructionPaths -ProjectProfile $ProjectProfile) {
        if ($instruction -notmatch '(?:^|/)(?:AGENTS(?:\.override)?|CLAUDE)\.md$') { continue }
        $directory = $instruction -replace '(?:^|/)[^/]+$', ''
        if (-not $directory -or $relative -eq $directory -or $relative.StartsWith($directory + '/', [System.StringComparison]::OrdinalIgnoreCase)) {
            $instruction
        }
    }
}

function Find-ProjectSource {
    param(
        [string] $Query,
        [ValidateRange(1, 100)][int] $Limit = 20,
        [string] $Root = $script:RepositoryRoot
    )

    if ([string]::IsNullOrWhiteSpace($Query)) { throw 'find requires -Query.' }
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Repository root does not exist: $Root" }
    $Root = (Resolve-Path -LiteralPath $Root).Path

    $files = @(Get-RepositoryFiles -Root $Root)
    $searchResults = New-Object System.Collections.ArrayList
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $files) {
        $path = Get-RelativeRepositoryPath -Path $file.FullName -Root $Root
        if ($path.IndexOf($Query, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            [void] $seen.Add($file.FullName)
            [void] $searchResults.Add([pscustomobject]@{ Path = $path; Match = 'path' })
            if ($searchResults.Count -ge $Limit) { break }
        }
    }

    # Content search runs only when the path pass left room under -Limit, so start from
    # the pass that always runs and let each branch name the tool it actually used.
    $searchTool = 'path'
    $ripgrep = Get-FirstCommand -Names @('rg')
    if ($searchResults.Count -lt $Limit -and $null -ne $ripgrep) {
        $searchTool = 'ripgrep'
        $ignoreGlobs = Get-RepositorySearchGlobs
        $output = @(& $ripgrep.Source '--files-with-matches' '--hidden' '--fixed-strings' '--ignore-case' '--no-messages' '--max-filesize' '1M' '--sort' 'path' @ignoreGlobs '--' $Query $Root 2>$null)
        if ($LASTEXITCODE -gt 1) { throw "ripgrep failed while searching for '$Query'." }
        foreach ($line in $output) {
            $fullPath = [string] $line
            if ([string]::IsNullOrWhiteSpace($fullPath) -or
                -not (Test-IsRepositoryPath -Path $fullPath -Root $Root) -or
                -not (Test-IsAgentSafePath -Path $fullPath -Root $Root) -or
                -not $seen.Add($fullPath)) { continue }
            [void] $searchResults.Add([pscustomobject]@{ Path = Get-RelativeRepositoryPath -Path $fullPath -Root $Root; Match = 'content' })
            if ($searchResults.Count -ge $Limit) { break }
        }
    }
    elseif ($searchResults.Count -lt $Limit) {
        $searchTool = 'Select-String'
        foreach ($file in $files) {
            if ($seen.Contains($file.FullName) -or -not (Test-IsSearchableText -File $file)) { continue }
            try {
                if (Select-String -LiteralPath $file.FullName -SimpleMatch -CaseSensitive:$false -Quiet -Pattern $Query) {
                    [void] $seen.Add($file.FullName)
                    [void] $searchResults.Add([pscustomobject]@{ Path = Get-RelativeRepositoryPath -Path $file.FullName -Root $Root; Match = 'content' })
                    if ($searchResults.Count -ge $Limit) { break }
                }
            }
            catch { continue }
        }
    }

    foreach ($result in $searchResults | Where-Object { $_.Match -eq 'content' }) {
        $match = Select-String -LiteralPath (Join-Path $Root $result.Path) -SimpleMatch -Pattern $Query | Select-Object -First 1
        $result | Add-Member -NotePropertyName Line -NotePropertyValue $match.LineNumber
    }
    return [pscustomobject]@{ Status = 'ok'; Query = $Query; Tool = $searchTool; Results = @($searchResults); Count = $searchResults.Count; Limit = $Limit; LimitReached = $searchResults.Count -ge $Limit; ContentMaxBytes = 1MB }
}

function Test-IsSearchableText {
    param([System.IO.FileInfo] $File)

    if ($File.Length -gt 1MB) { return $false }
    $stream = [System.IO.File]::OpenRead($File.FullName)
    try {
        $buffer = [byte[]]::new(8192)
        $count = $stream.Read($buffer, 0, $buffer.Length)
        for ($index = 0; $index -lt $count; $index++) {
            if ($buffer[$index] -eq 0) { return $false }
        }
        return $true
    }
    finally { $stream.Dispose() }
}
