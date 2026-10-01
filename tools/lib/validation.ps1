function Get-PowerShellLintOutcome {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    $scripts = @($ProjectProfile.Files | Where-Object { $_.Extension -in @('.ps1', '.psm1', '.psd1') })
    $failures = New-Object System.Collections.ArrayList
    foreach ($file in $scripts) {
        $errors = $null
        [void] [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref] $null, [ref] $errors)
        if ($errors -and $errors.Count -gt 0) {
            $relativePath = Get-RelativeRepositoryPath -Path $file.FullName -Root $ProjectProfile.Root
            [void] $failures.Add("${relativePath}:$($errors[0].Extent.StartLineNumber) $($errors[0].Message)")
        }
    }

    $warnings = New-Object System.Collections.ArrayList
    $unavailable = $false
    $analyzer = @(Get-BuildModule -Name PSScriptAnalyzer)
    if ($analyzer.Count -gt 0) {
        Import-Module -Name $analyzer[0].Path
        $settingsFile = Join-Path $ProjectProfile.Root 'PSScriptAnalyzerSettings.psd1'
        $analyzerArgs = @{}
        if (Test-Path -LiteralPath $settingsFile -PathType Leaf) { $analyzerArgs['Settings'] = $settingsFile }
        else { $analyzerArgs['Severity'] = @('Error', 'Warning') }

        # Analyze the enumerated list rather than recursing from the root, so the analyzer
        # cannot reach into secrets/ or the sensitive files the walker already dropped.
        foreach ($file in $scripts) {
            $relativePath = Get-RelativeRepositoryPath -Path $file.FullName -Root $ProjectProfile.Root
            foreach ($finding in @(Invoke-ScriptAnalyzer -Path $file.FullName @analyzerArgs)) {
                [void] $failures.Add("${relativePath}:$($finding.Line) $($finding.RuleName)")
            }
        }
    }
    else {
        $unavailable = $true
        [void] $warnings.Add('The required PSScriptAnalyzer version is not installed (run bootstrap -Install).')
    }

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("$($scripts.Count) PowerShell file(s) parsed; analyzer available: $(-not $unavailable)")
        Warnings = @($warnings)
        Unavailable = $unavailable
    }
}

function Get-PesterOutcome {
    param([Parameter(Mandatory = $true)][string[]] $TestPath)

    $pester = @(Get-BuildModule -Name Pester -Loaded)
    if ($pester.Count -eq 0) {
        return [pscustomobject]@{
            Failures = @()
            Summary = @('Pester could not run because Pester 5.x is not installed.')
            Warnings = @('Pester 5.x is required (run bootstrap -Install).')
            Unavailable = $true
        }
    }

    # Verbosity None keeps Pester off the host: this command's contract is one JSON
    # document on stdout, and Pester's normal output would be printed beside it.
    $configuration = New-PesterConfiguration
    $configuration.Run.Path = $TestPath
    $configuration.Run.PassThru = $true
    $configuration.Run.Exit = $false
    $configuration.Run.Throw = $false
    $configuration.Output.Verbosity = 'None'
    $result = Invoke-Pester -Configuration $configuration

    $failures = New-Object System.Collections.ArrayList
    # Discovery and BeforeAll errors can fail a container without failing an It block.
    foreach ($container in @($result.Containers | Where-Object { $_.Result -eq 'Failed' })) {
        foreach ($record in @($container.ErrorRecord)) {
            [void] $failures.Add("$($container.Item): $($record.Exception.Message)")
        }
    }
    foreach ($test in @($result.Failed | Select-Object -First 10)) {
        $reason = @("$($test.ErrorRecord.Exception.Message)" -split "`r?`n" | Where-Object { $_ } | Select-Object -First 1)
        [void] $failures.Add("$($test.ExpandedPath): $reason")
    }
    if ($result.FailedCount -gt $failures.Count) {
        [void] $failures.Add("... and $($result.FailedCount - $failures.Count) further failing test(s)")
    }
    if ($result.Result -eq 'Failed' -and $failures.Count -eq 0) { [void] $failures.Add('Pester failed during discovery or setup. Inspect the test container.') }
    $incomplete = $result.TotalCount -eq 0 -or $result.SkippedCount -gt 0 -or $result.NotRunCount -gt 0

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("Pester: $($result.PassedCount) passed, $($result.FailedCount) failed, $($result.SkippedCount) skipped")
        Warnings = if ($incomplete) { @('Pester discovered no tests or left tests skipped/not run.') } else { @() }
        Unavailable = $incomplete
    }
}

function Get-ConfigurationOutcome {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    $configFiles = @($ProjectProfile.Files | Where-Object { $_.Extension -in $script:ConfigurationExtensions })
    $failures = New-Object System.Collections.ArrayList
    foreach ($file in $configFiles) {
        $relativePath = Get-RelativeRepositoryPath -Path $file.FullName -Root $ProjectProfile.Root
        try {
            if ($file.Extension -eq '.json') { [void] (ConvertFrom-StrictJson -Text (Get-Content -LiteralPath $file.FullName -Raw)) }
            else { [void] (Get-SafeXmlDocument -Path $file.FullName) }
        }
        catch {
            [void] $failures.Add("${relativePath}: $($_.Exception.Message)")
        }
    }

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("$($configFiles.Count) configuration file(s) validated")
        Warnings = @()
    }
}

# Tracks fenced code blocks line by line. Returns $true for a line that opens, closes or lies
# inside a block; $State holds the opening marker between calls.
function Test-MarkdownCodeLine {
    param([AllowEmptyString()][string] $Text, [Parameter(Mandatory = $true)][hashtable] $State)

    if ($Text -match '^ {0,3}(`{3,}|~{3,})') {
        $marker = $matches[1]
        if ($null -eq $State.Fence) { $State.Fence = $marker }
        elseif ($marker[0] -eq $State.Fence[0] -and $marker.Length -ge $State.Fence.Length) { $State.Fence = $null }
        return $true
    }
    return $null -ne $State.Fence
}

# The anchors a Markdown file offers: GitHub's heading slugs (lower case, punctuation dropped,
# spaces to hyphens, repeats numbered) and explicit <a id> or <a name> targets.
function Get-MarkdownAnchor {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowEmptyString()][string[]] $Line)

    $anchors = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $repeats = @{}
    $state = @{ Fence = $null }
    foreach ($text in $Line) {
        if (Test-MarkdownCodeLine -Text $text -State $state) { continue }
        foreach ($match in [regex]::Matches($text, '<a\s+(?:id|name)="([^"]+)"')) { [void] $anchors.Add($match.Groups[1].Value) }
        if ($text -notmatch '^ {0,3}#{1,6}\s+(.*?)\s*#*\s*$') { continue }
        $heading = [regex]::Replace($matches[1], '\[([^\]]*)\]\([^)]*\)', '$1')
        $slug = [regex]::Replace($heading.ToLowerInvariant(), '[^\p{L}\p{N}\p{M} _-]', '').Replace(' ', '-')
        if ($repeats.ContainsKey($slug)) { $repeats[$slug]++; $slug = "$slug-$($repeats[$slug])" }
        else { $repeats[$slug] = 0 }
        [void] $anchors.Add($slug)
    }
    return , $anchors
}

# The links and repository paths that a Markdown file names outside its code blocks. A path is
# a code span that starts with a repository directory and holds no placeholder.
function Get-MarkdownReference {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowEmptyString()][string[]] $Line)

    $roots = ($script:RepositoryPathRoots | ForEach-Object { [regex]::Escape($_) }) -join '|'
    $pathPattern = '^(?:' + $roots + ')/[^\s<>*{}$|?":…]*$'
    $state = @{ Fence = $null }
    for ($index = 0; $index -lt $Line.Count; $index++) {
        $text = $Line[$index]
        if (Test-MarkdownCodeLine -Text $text -State $state) { continue }
        $paths = New-Object System.Collections.ArrayList
        $prose = $text
        foreach ($span in [regex]::Matches($text, '(`+)(.+?)\1')) {
            # Blank the span, so that link syntax quoted as code is not read as a link.
            $prose = $prose.Remove($span.Index, $span.Length).Insert($span.Index, ' ' * $span.Length)
            $path = $span.Groups[2].Value.Trim().Replace('\', '/') -replace '^\./', ''
            if ($path -match $pathPattern -and -not $path.Contains('...')) { [void] $paths.Add($path) }
        }
        $targets = @([regex]::Matches($prose, '\]\(\s*(<[^>]*>|[^()\s]+)(?:\s+(?:"[^"]*"|''[^'']*''))?\s*\)') | ForEach-Object { $_.Groups[1].Value })
        # A reference definition; a footnote ([^1]: text) is not one.
        if ($prose -match '^ {0,3}\[(?!\^)[^\]]+\]:\s*(\S+)') { $targets += $matches[1] }
        foreach ($target in $targets) {
            [pscustomobject]@{ Line = $index + 1; Kind = 'link'; Target = $target.Trim('<', '>') }
        }
        foreach ($path in $paths) {
            [pscustomobject]@{ Line = $index + 1; Kind = 'path'; Target = $path }
        }
    }
}

# Every relative link, heading anchor and repository path in maintained Markdown must resolve,
# so that instructions and guides cannot point an agent or a reader at a file that is gone.
function Get-DocumentationOutcome {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    $root = $ProjectProfile.Root
    $failures = New-Object System.Collections.ArrayList
    $anchors = @{}
    $links = 0
    $paths = 0
    $documents = @(
        $ProjectProfile.Files | Where-Object { $_.Extension -eq '.md' } |
            ForEach-Object { [pscustomobject]@{ File = $_; Path = Get-RelativeRepositoryPath -Path $_.FullName -Root $root } } |
            Where-Object { $_.Path -notmatch $script:FrozenDocumentationPattern }
    )
    [array]::Sort($documents, [System.Comparison[object]] { param($left, $right) [string]::CompareOrdinal($left.Path, $right.Path) })
    foreach ($document in $documents) {
        $dated = $document.Path -match $script:DatedDocumentationPattern
        foreach ($reference in Get-MarkdownReference -Line @(Get-Content -LiteralPath $document.File.FullName)) {
            $location = "$($document.Path):$($reference.Line)"
            if ($reference.Kind -eq 'path') {
                if ($dated) { continue }
                $paths++
                if (-not (Test-Path -LiteralPath (Join-Path $root $reference.Target.TrimEnd('/')))) {
                    [void] $failures.Add("${location}: path '$($reference.Target)' does not exist.")
                }
                continue
            }
            # A scheme means an external target (https, mailto, file), which is not fetched.
            if ($reference.Target -match '^[A-Za-z][A-Za-z0-9+.-]*:') { continue }
            $links++
            $target, $anchor = $reference.Target -split '#', 2
            $resolved = $document.File.FullName
            if ($target) {
                try { $resolved = [System.IO.Path]::GetFullPath((Join-Path $document.File.DirectoryName ([uri]::UnescapeDataString($target)))) }
                catch { $resolved = $null }
                if (-not $resolved -or -not (Test-IsRepositoryPath -Path $resolved -Root $root) -or
                    -not (Test-IsAgentSafePath -Path $resolved -Root $root) -or -not (Test-Path -LiteralPath $resolved)) {
                    [void] $failures.Add("${location}: link '$($reference.Target)' does not resolve.")
                    continue
                }
            }
            if (-not $anchor -or $resolved -notlike '*.md' -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)) { continue }
            if (-not $anchors.ContainsKey($resolved)) { $anchors[$resolved] = Get-MarkdownAnchor -Line @(Get-Content -LiteralPath $resolved) }
            if (-not $anchors[$resolved].Contains([uri]::UnescapeDataString($anchor))) {
                [void] $failures.Add("${location}: link '$($reference.Target)' names no heading of its target.")
            }
        }
    }

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("$($documents.Count) Markdown file(s), $links link(s) and $paths path reference(s) checked")
        Warnings = @()
    }
}

# name and description from the front matter of a SKILL.md file.
function Get-SkillFrontMatter {
    param([Parameter(Mandatory = $true)][string] $Path)

    $result = @{ name = $null; description = $null }
    $lines = @(Get-Content -LiteralPath $Path)
    if ($lines.Count -eq 0 -or $lines[0] -ne '---') { return $result }
    foreach ($line in $lines | Select-Object -Skip 1) {
        if ($line -eq '---') { break }
        if ($line -match '^(name|description):\s*(.*)$') { $result[$matches[1]] = $matches[2].Trim() }
    }
    return $result
}

# A skill has one body, under .agents/skills, and a Claude wrapper with the same name and
# description under .claude/skills, so both agents select and run the same procedure.
function Get-SkillLayoutOutcome {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    $failures = New-Object System.Collections.ArrayList
    $skills = [ordered]@{}
    foreach ($file in $ProjectProfile.Files | Where-Object { $_.Name -ceq 'SKILL.md' }) {
        $relativePath = Get-RelativeRepositoryPath -Path $file.FullName -Root $ProjectProfile.Root
        if ($relativePath -notmatch '^(\.agents|\.claude)/skills/([^/]+)/SKILL\.md$') { continue }
        $agent = $matches[1]
        $name = $matches[2]
        $frontMatter = Get-SkillFrontMatter -Path $file.FullName
        if ($frontMatter.name -cne $name) { [void] $failures.Add("${relativePath}: the front matter name must be '$name'.") }
        if (-not $skills.Contains($name)) { $skills[$name] = @{} }
        $skills[$name][$agent] = [pscustomobject]@{ Path = $relativePath; Description = $frontMatter.description; File = $file }
    }
    foreach ($name in $skills.Keys) {
        $body = $skills[$name]['.agents']
        $wrapper = $skills[$name]['.claude']
        if ($null -eq $body) { [void] $failures.Add("Skill '$name' has no body at .agents/skills/$name/SKILL.md."); continue }
        if ($null -eq $wrapper) { [void] $failures.Add("Skill '$name' has no Claude wrapper at .claude/skills/$name/SKILL.md."); continue }
        if ([string]::IsNullOrWhiteSpace($body.Description) -or $body.Description -cne $wrapper.Description) {
            [void] $failures.Add("Skill '$name' has different descriptions in .agents and .claude.")
        }
        if (-not (Get-Content -LiteralPath $wrapper.File.FullName -Raw).Contains(".agents/skills/$name/SKILL.md")) {
            [void] $failures.Add("$($wrapper.Path): the wrapper must name .agents/skills/$name/SKILL.md.")
        }
    }
    return [pscustomobject]@{ Failures = @($failures); Count = $skills.Count }
}

function Get-ObjectStringValues {
    param([object] $InputObject)

    if ($null -eq $InputObject) { return }
    if ($InputObject -is [string]) {
        Write-Output $InputObject
        return
    }
    if ($InputObject -is [System.Collections.IDictionary]) {
        foreach ($value in $InputObject.Values) { Get-ObjectStringValues -InputObject $value }
        return
    }
    if ($InputObject -is [System.Collections.IEnumerable]) {
        foreach ($value in $InputObject) { Get-ObjectStringValues -InputObject $value }
        return
    }

    foreach ($property in $InputObject.PSObject.Properties | Where-Object { $_.MemberType -eq 'NoteProperty' }) {
        Get-ObjectStringValues -InputObject $property.Value
    }
}

function Get-ToolingLayoutOutcome {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    $failures = New-Object System.Collections.ArrayList
    $hookScripts = New-Object System.Collections.ArrayList
    $root = $ProjectProfile.Root
    $toolsRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'tools')).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $toolsPrefix = $toolsRoot + [System.IO.Path]::DirectorySeparatorChar

    $settingsFile = Join-Path $root '.claude\settings.json'
    if (Test-Path -LiteralPath $settingsFile -PathType Leaf) {
        if (-not (Test-IsSafeRepositoryFile -File (Get-Item -LiteralPath $settingsFile -Force) -Root $root)) {
            return [pscustomobject]@{ Failures = @('.claude/settings.json must be a regular repository file.'); Summary = @(); Warnings = @() }
        }
        try {
            $settings = ConvertFrom-StrictJson -Text (Get-Content -LiteralPath $settingsFile -Raw)
            if ($settings -isnot [pscustomobject]) { throw 'Settings must be a JSON object.' }
            $hooksProperty = $settings.PSObject.Properties['hooks']
            if ($null -ne $hooksProperty) {
                if ($hooksProperty.Value -isnot [pscustomobject]) { throw 'hooks must be an object of event arrays.' }
                foreach ($hookEvent in $hooksProperty.Value.PSObject.Properties) {
                    foreach ($group in @($hookEvent.Value)) {
                        foreach ($hook in @($group.hooks)) {
                            if ($hook.type -eq 'command' -and [string]::IsNullOrWhiteSpace([string]$hook.command)) { throw 'Command hooks require a nonempty command.' }
                        }
                    }
                }
                $hookScripts = @(
                    Get-ObjectStringValues -InputObject $hooksProperty.Value |
                        Where-Object { $_ -match '(?i)\.ps1$' } |
                        Sort-Object -Unique
                )
            }
        }
        catch {
            [void]$failures.Add(".claude/settings.json: $($_.Exception.Message)")
            $hookScripts = @()
        }
    }

    foreach ($hookScript in $hookScripts) {
        $relativePath = $hookScript.Replace('\', '/')
        $relativePath = $relativePath -replace '^\$\{CLAUDE_PROJECT_DIR\}/?', ''
        if ($relativePath -notmatch '^tools/') {
            [void] $failures.Add("Hook helper '$hookScript' must live under tools/.")
            continue
        }
        try {
            $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
        }
        catch {
            [void] $failures.Add("Hook helper '$hookScript' has an invalid path.")
            continue
        }
        if (-not $resolvedPath.StartsWith($toolsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            [void] $failures.Add("Hook helper '$hookScript' must resolve under tools/.")
            continue
        }
        if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
            [void] $failures.Add("Hook helper '$hookScript' does not exist.")
            continue
        }
        $item = Get-Item -LiteralPath $resolvedPath -Force
        if (-not (Test-IsSafeRepositoryFile -File $item -Root $root)) {
            [void] $failures.Add("Hook helper '$hookScript' must be a regular, safe repository file.")
        }
    }

    foreach ($file in $ProjectProfile.Files | Where-Object { $_.Name -eq 'CLAUDE.md' -or $_.FullName -like '*\.claude\rules\*.md' }) {
        foreach ($line in Get-Content -LiteralPath $file.FullName) {
            if ($line -notmatch '^@([^\r\n]+)$') { continue }
            $import = $matches[1].Trim()
            $target = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $import))
            if (-not (Test-IsRepositoryPath -Path $target -Root $root) -or
                -not (Test-Path -LiteralPath $target -PathType Leaf) -or
                -not (Test-IsSafeRepositoryFile -File (Get-Item -LiteralPath $target -Force) -Root $root)) {
                [void]$failures.Add("$(Get-RelativeRepositoryPath -Path $file.FullName -Root $root): import '$import' must name a regular repository file.")
            }
        }
    }

    $skillLayout = Get-SkillLayoutOutcome -ProjectProfile $ProjectProfile
    foreach ($failure in $skillLayout.Failures) { [void] $failures.Add($failure) }

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("$($hookScripts.Count) configured PowerShell hook helper(s) checked", "$($skillLayout.Count) skill(s) paired between .agents and .claude")
        Warnings = @()
    }
}

# The GitHub workflow files, as repository paths.
function Get-WorkflowFile {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    @($ProjectProfile.Files |
            ForEach-Object { Get-RelativeRepositoryPath -Path $_.FullName -Root $ProjectProfile.Root } |
            Where-Object { $_ -match '^\.github/workflows/[^/]+\.ya?ml$' })
}

# actionlint checks the YAML, the workflow schema and the expressions of every workflow. Nothing
# else reads these files before GitHub runs them, and the release workflow runs on the push
# that publishes.
function Get-WorkflowLintOutcome {
    param([Parameter(Mandatory = $true)][object] $ProjectProfile)

    $workflows = @(Get-WorkflowFile -ProjectProfile $ProjectProfile)
    $command = Get-FirstCommand -Names @('actionlint')
    if ($null -eq $command) {
        return [pscustomobject]@{
            Failures = @()
            Summary = @('The workflow files could not be linted because actionlint is not installed.')
            Warnings = @('actionlint is required (run bootstrap -Install).')
            Unavailable = $true
        }
    }

    # shellcheck and pyflakes are switched off: every run block is PowerShell, and the result
    # must not depend on whether either happens to be installed.
    $arguments = @('-no-color', '-oneline', '-shellcheck=', '-pyflakes=') + $workflows
    $result = Invoke-BoundedProcess -Executable $command.Source -Arguments $arguments -WorkingDirectory $ProjectProfile.Root -TimeoutSeconds 120
    $lines = @($result.Lines | ForEach-Object { ([string] $_).Trim() } | Where-Object { $_ })
    $failures = @()
    if ($result.ExitCode -ne 0) {
        $failures = if ($lines.Count -gt 0) { $lines } else { @("actionlint exited with $($result.ExitCode).") }
    }

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("$($workflows.Count) workflow file(s) linted with actionlint")
        Warnings = @()
    }
}

# The in-process stages: tooling lint and tests, configuration syntax, documentation
# references, the Claude and Codex layout, and the workflow lint.
function Get-BuiltinValidationPlan {
    param(
        [Parameter(Mandatory = $true)][object] $ProjectProfile,
        [switch] $SkipTests
    )

    $plan = New-Object System.Collections.ArrayList

    # Each in-process stage is a plain script block plus its arguments, never a closure.
    # GetNewClosure rebinds a script block to a fresh dynamic module whose scope chain
    # excludes this script, so the stage functions below resolve only when dev.ps1 is
    # started with -File and fail with "term is not recognized" when it is called with &.
    if ($ProjectProfile.Stack -contains 'powershell') {
        [void] $plan.Add([pscustomobject]@{
                Name = 'powershell-lint'
                Action = { param([object] $ProjectProfile) Get-PowerShellLintOutcome -ProjectProfile $ProjectProfile }
                ActionArguments = @{ ProjectProfile = $ProjectProfile }
            })
    }

    $powerShellTests = @(
        $ProjectProfile.TestFiles |
            Where-Object { $_ -match '\.Tests\.ps1$' } |
            ForEach-Object { Join-Path $ProjectProfile.Root $_ }
    )
    if ($powerShellTests.Count -gt 0 -and -not $SkipTests) {
        [void] $plan.Add([pscustomobject]@{
                Name = 'powershell-test'
                Action = { param([string[]] $TestPath) Get-PesterOutcome -TestPath $TestPath }
                ActionArguments = @{ TestPath = $powerShellTests }
            })
    }

    if (@($ProjectProfile.Files | Where-Object { $_.Extension -in $script:ConfigurationExtensions }).Count -gt 0) {
        [void] $plan.Add([pscustomobject]@{
                Name = 'configuration'
                Action = { param([object] $ProjectProfile) Get-ConfigurationOutcome -ProjectProfile $ProjectProfile }
                ActionArguments = @{ ProjectProfile = $ProjectProfile }
            })
    }

    if (@($ProjectProfile.Files | Where-Object { $_.Extension -eq '.md' }).Count -gt 0) {
        [void] $plan.Add([pscustomobject]@{
                Name = 'documentation'
                Action = { param([object] $ProjectProfile) Get-DocumentationOutcome -ProjectProfile $ProjectProfile }
                ActionArguments = @{ ProjectProfile = $ProjectProfile }
            })
    }

    $toolingSettings = Join-Path $ProjectProfile.Root '.claude\settings.json'
    $customCheck = Join-Path $ProjectProfile.Root 'tools\check.ps1'
    if ((Test-Path -LiteralPath $toolingSettings -PathType Leaf) -or (Test-Path -LiteralPath $customCheck -PathType Leaf)) {
        [void] $plan.Add([pscustomobject]@{
                Name = 'tooling-layout'
                Action = { param([object] $ProjectProfile) Get-ToolingLayoutOutcome -ProjectProfile $ProjectProfile }
                ActionArguments = @{ ProjectProfile = $ProjectProfile }
            })
    }

    if (@(Get-WorkflowFile -ProjectProfile $ProjectProfile).Count -gt 0) {
        [void] $plan.Add([pscustomobject]@{
                Name = 'workflow-lint'
                Action = { param([object] $ProjectProfile) Get-WorkflowLintOutcome -ProjectProfile $ProjectProfile }
                ActionArguments = @{ ProjectProfile = $ProjectProfile }
            })
    }

    return @($plan)
}

function New-UnavailableStage {
    param([string] $Name, [string] $Reason)
    return [pscustomobject]@{
        Name = $Name
        Reason = $Reason
        Action = { param($Reason) [pscustomobject]@{ Failures = @(); Summary = @($Reason); Warnings = @(); Unavailable = $true } }
        ActionArguments = @{ Reason = $Reason }
    }
}

function Get-ValidationPlan {
    param(
        [Parameter(Mandatory = $true)][object] $ProjectProfile,
        [switch] $SkipTests
    )

    # The built-in stages check the tooling; tools/check.ps1 is the product gate (build, Core
    # tests, staged package, product Pester).
    $builtins = @(Get-BuiltinValidationPlan -ProjectProfile $ProjectProfile -SkipTests:$SkipTests)
    $checkScript = Join-Path $ProjectProfile.Root 'tools\check.ps1'
    if (Test-Path -LiteralPath $checkScript -PathType Leaf) {
        $file = Get-Item -LiteralPath $checkScript -Force
        if (-not (Test-IsSafeRepositoryFile -File $file -Root $ProjectProfile.Root)) { throw 'tools/check.ps1 must be a regular repository file.' }
        return $builtins + @([pscustomobject]@{
                Name = 'project-check'
                Executable = Join-Path $PSHOME 'pwsh.exe'
                Arguments = @('-NoProfile', '-File', $checkScript) + $(if ($SkipTests) { @('-SkipTests') } else { @() })
                # The gate speaks this command's exit codes: 0 pass, 1 failure, 2 incomplete.
                ExitCodeContract = 'dev'
                # Build, two Core test runs, packaging with help, and product Pester.
                TimeoutSeconds = 900
            })
    }
    # Product code without its gate cannot be verified; say so instead of passing on the built-ins.
    if ($ProjectProfile.Stack -contains 'dotnet') {
        return $builtins + @(New-UnavailableStage -Name 'project-check' -Reason 'tools/check.ps1 is missing, so the product gate cannot run.')
    }

    return $builtins
}

function Get-ProjectValidationPlan {
    param([string] $Root = $script:RepositoryRoot, [switch] $SkipTests)
    $projectProfile = Get-ProjectProfile -Root $Root
    $stages = @(Get-ValidationPlan -ProjectProfile $projectProfile -SkipTests:$SkipTests)
    return [pscustomobject]@{
        Status = 'ok'
        Stages = @(
            foreach ($stage in $stages) {
                $entry = [ordered]@{ Name = $stage.Name }
                foreach ($key in @('Executable', 'Arguments', 'Reason')) {
                    $property = $stage.PSObject.Properties[$key]
                    if ($property) { $entry[$key] = $property.Value }
                }
                [pscustomobject]$entry
            }
        )
        Notes = @('Preview only. verify -Stage <name> runs the named stages and always reports incomplete.', 'Dependencies must already be installed and restored; verify never installs them.')
    }
}

function Invoke-ValidationStage {
    param(
        [Parameter(Mandatory = $true)][object] $Stage,
        [Parameter(Mandatory = $true)][string] $Root
    )

    # An in-process stage returns its own outcome instead of an exit code, so it can report
    # per-finding detail without a child process and without printing beside this command's
    # JSON. External stages keep the exit-code path below.
    $action = $Stage.PSObject.Properties['Action']
    if ($null -ne $action -and $null -ne $action.Value) {
        $actionArguments = @{}
        $actionArgumentProperty = $Stage.PSObject.Properties['ActionArguments']
        if ($null -ne $actionArgumentProperty -and $null -ne $actionArgumentProperty.Value) {
            $actionArguments = $actionArgumentProperty.Value
        }

        Push-Location -LiteralPath $Root
        try {
            $outcome = & $action.Value @actionArguments
        }
        catch {
            return [pscustomobject]@{ Name = $Stage.Name; Status = 'fail'; ExitCode = 1; Summary = @($_.Exception.Message); Warnings = @(); AdvisoryWarnings = $false }
        }
        finally {
            Pop-Location
        }

        $stageFailures = @($outcome.Failures)
        $unavailableProperty = $outcome.PSObject.Properties['Unavailable']
        $isUnavailable = $null -ne $unavailableProperty -and [bool] $unavailableProperty.Value
        return [pscustomobject]@{
            Name = $Stage.Name
            Status = if ($stageFailures.Count -gt 0) { 'fail' } elseif ($isUnavailable) { 'unavailable' } else { 'pass' }
            ExitCode = if ($stageFailures.Count -gt 0) { 1 } elseif ($isUnavailable) { 2 } else { 0 }
            Summary = @(@($stageFailures) + @($outcome.Summary) | Select-Object -First 12)
            Warnings = @($outcome.Warnings)
            # A stage that ran in-process reports its own state, so a warning here means
            # the check really could not complete.
            AdvisoryWarnings = $false
        }
    }

    $executable = [string] $Stage.Executable
    $command = Get-FirstCommand -Names @($executable)
    if ($null -eq $command -and -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        $missingExecutable = "Missing executable: $($Stage.Executable)"
        return [pscustomobject]@{ Name = $Stage.Name; Status = 'unavailable'; ExitCode = $null; Summary = @($missingExecutable); Warnings = @($missingExecutable); AdvisoryWarnings = $false }
    }

    if ($null -ne $command) { $executable = $command.Source }
    try {
        $timeout = 300
        $timeoutProperty = $Stage.PSObject.Properties['TimeoutSeconds']
        if ($timeoutProperty) { $timeout = [int]$timeoutProperty.Value }
        $native = Invoke-BoundedProcess -Executable $executable -Arguments $Stage.Arguments -WorkingDirectory $Root -TimeoutSeconds $timeout
        $output = $native.Lines
        $exitCode = $native.ExitCode
    }
    catch {
        return [pscustomobject]@{ Name = $Stage.Name; Status = 'fail'; ExitCode = 1; Summary = @($_.Exception.Message); Warnings = @(); AdvisoryWarnings = $false }
    }

    # Strip ANSI colour before matching or reporting: a stage that decides it is writing
    # to a terminal would otherwise embed escape sequences in this command's JSON, and
    # break the keyword matching below on any line it coloured.
    $lines = @(
        $output |
            ForEach-Object { ([string] $_) -replace "`e\[[0-9;]*[a-zA-Z]", '' } |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ }
    )
    # A stage that opts into this command's own exit-code contract reports 2 for
    # "could not fully check", which is incomplete rather than a failure.
    $usesDevExitCodes = $false
    $contract = $Stage.PSObject.Properties['ExitCodeContract']
    if ($null -ne $contract -and [string] $contract.Value -eq 'dev') { $usesDevExitCodes = $true }

    $status = if ($exitCode -eq 0) { 'pass' } elseif ($usesDevExitCodes -and $exitCode -eq 2) { 'unavailable' } else { 'fail' }
    $warnings = @($lines | Where-Object { $_ -match '(?i)(^skipped\s*\(|\bnot installed\b|\bmissing\b)' })
    # The gate marks its own result lines with "check: ". A passing gate is summarized by them,
    # so the summary names every step that ran instead of the last lines a runner printed.
    $marked = @($lines | Where-Object { $_ -cmatch '^check: \S' } | ForEach-Object { $_.Substring(7) })
    if ($status -eq 'pass') {
        $summary = @($marked | Select-Object -Last 12)
        if ($summary.Count -eq 0) { $summary = @($lines | Where-Object { $_ -match '(?i)(check passed|tests passed|build succeeded|test run successful)' } | Select-Object -Last 3) }
        if ($summary.Count -eq 0) { $summary = @($lines | Select-Object -Last 3) }
    }
    else {
        $important = @($lines | Where-Object { $_ -match '(?i)(fail|error|exception|warning|skipped|summary|not installed|missing)' })
        $summary = @($important + ($lines | Select-Object -Last 8) | Select-Object -Unique | Select-Object -Last 12)
    }
    return [pscustomobject]@{
        Name = $Stage.Name
        Status = $status
        ExitCode = $exitCode
        Summary = $summary
        Warnings = $warnings
        # Scraped from human-oriented output, so these report but do not decide: a build
        # that succeeded while printing "missing peer dependency" is not an incomplete run.
        AdvisoryWarnings = $true
    }
}

function Invoke-BoundedProcess {
    param([string] $Executable, [string[]] $Arguments, [string] $WorkingDirectory, [ValidateRange(1, 3600)][int] $TimeoutSeconds = 300)

    # A fixed child program accepts data on stdin. This also runs Windows .cmd shims
    # without interpolating paths or arguments into shell source.
    $program = @'
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
try {
    $stage = [Console]::In.ReadToEnd() | ConvertFrom-Json
    Set-Location -LiteralPath $stage.WorkingDirectory
    $env:CI = 'true'
    $env:NO_COLOR = '1'
    $global:LASTEXITCODE = 0
    & $stage.Executable @($stage.Arguments) *>&1 | ForEach-Object { [Console]::Out.WriteLine([string]$_) }
    exit $LASTEXITCODE
}
catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
'@
    $info = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh.exe'))
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-NonInteractive', '-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($program)))) { $info.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $info
    $lines = [System.Collections.Generic.Queue[string]]::new()
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $started = $false
    try {
        [void]$process.Start()
        $started = $true
        $process.StandardInput.WriteLine((@{ Executable = $Executable; Arguments = @($Arguments); WorkingDirectory = $WorkingDirectory } | ConvertTo-Json -Compress))
        $process.StandardInput.Close()
        $readers = @($process.StandardOutput, $process.StandardError)
        $pending = @($readers[0].ReadLineAsync(), $readers[1].ReadLineAsync())
        while ($null -ne $pending[0] -or $null -ne $pending[1]) {
            if ($watch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { $process.Kill($true); throw "Stage timed out after $TimeoutSeconds seconds." }
            $progress = $false
            for ($index = 0; $index -lt 2; $index++) {
                if ($null -eq $pending[$index] -or -not $pending[$index].IsCompleted) { continue }
                $line = $pending[$index].GetAwaiter().GetResult()
                if ($null -eq $line) { $pending[$index] = $null; continue }
                $lines.Enqueue($line.Substring(0, [Math]::Min(2000, $line.Length)))
                if ($lines.Count -gt 120) { [void]$lines.Dequeue() }
                $pending[$index] = $readers[$index].ReadLineAsync()
                $progress = $true
            }
            if (-not $progress) { [System.Threading.Thread]::Sleep(10) }
        }
        $remaining = [Math]::Max(1, [int](($TimeoutSeconds - $watch.Elapsed.TotalSeconds) * 1000))
        if (-not $process.WaitForExit($remaining)) { $process.Kill($true); throw "Stage timed out after $TimeoutSeconds seconds." }
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Lines = @($lines) }
    }
    finally {
        if ($started -and -not $process.HasExited) { $process.Kill($true) }
        $process.Dispose()
    }
}

function Invoke-ProjectVerification {
    param(
        [string] $Root = $script:RepositoryRoot,
        [switch] $SkipTests,
        [string[]] $Stage
    )

    $projectProfile = Get-ProjectProfile -Root $Root
    $plan = @(Get-ValidationPlan -ProjectProfile $projectProfile -SkipTests:$SkipTests)
    if ($Stage) {
        foreach ($name in $Stage) {
            if ($name -notin $plan.Name) { throw "Unknown stage '$name'. Run dev.ps1 plan for valid names." }
        }
        $plan = @($plan | Where-Object { $_.Name -in $Stage })
    }
    if ($plan.Count -eq 0) {
        return [pscustomobject]@{ Status = 'unavailable'; Stages = @(); Warnings = @('Nothing to validate: no PowerShell, configuration or product files were found.') }
    }

    # Load the supported Pester before any stage runs. Linting a test file makes
    # PSScriptAnalyzer resolve Describe/It/Should, which auto-loads the *highest* installed
    # Pester; if that is a 6.x, importing 5.x afterwards fails with "assembly with same name
    # is already loaded" and the suite cannot run at all.
    if (@($plan | Where-Object { $_.Name -eq 'powershell-test' }).Count -gt 0) {
        $pesterModule = @(Get-BuildModule -Name Pester)
        $loadedPester = @(Get-BuildModule -Name Pester -Loaded)
        if ($pesterModule.Count -gt 0 -and $loadedPester.Count -eq 0) {
            Import-Module -Name $pesterModule[0].Path
        }
    }

    $stages = @(
        foreach ($item in $plan) {
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $result = Invoke-ValidationStage -Stage $item -Root $projectProfile.Root
            $result | Add-Member -NotePropertyName DurationMs -NotePropertyValue $watch.ElapsedMilliseconds
            $result
        }
    )

    # Every warning is reported, but only a stage that knows its own state can make the
    # run incomplete. Text scraped from a passing external stage reports and nothing more.
    $warnings = New-Object System.Collections.ArrayList
    $blockingWarningCount = 0
    foreach ($stageResult in $stages) {
        $advisory = $stageResult.PSObject.Properties['AdvisoryWarnings']
        $isAdvisory = $null -ne $advisory -and [bool] $advisory.Value
        foreach ($warning in $stageResult.Warnings) {
            [void] $warnings.Add("$($stageResult.Name): $warning")
            if (-not $isAdvisory) { $blockingWarningCount++ }
        }
    }
    if ($SkipTests) { [void] $warnings.Add('Tests were skipped by request.') }
    if ($Stage) { [void] $warnings.Add('Only the selected stages ran; run verify for the full gate.') }

    $failed = @($stages | Where-Object { $_.Status -eq 'fail' })
    $unavailable = @($stages | Where-Object { $_.Status -eq 'unavailable' })
    $status = if ($failed.Count -gt 0) { 'fail' }
    elseif ($unavailable.Count -gt 0 -or $blockingWarningCount -gt 0 -or $SkipTests -or $Stage) { 'incomplete' }
    else { 'pass' }

    return [pscustomobject]@{
        Status = $status
        # AdvisoryWarnings is plumbing for the status above, not part of the reported shape.
        Stages = @($stages | Select-Object -Property Name, Status, ExitCode, Summary, Warnings, DurationMs)
        Warnings = @($warnings)
    }
}
