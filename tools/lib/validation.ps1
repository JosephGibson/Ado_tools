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

    return [pscustomobject]@{
        Failures = @($failures)
        Summary = @("$($hookScripts.Count) configured PowerShell hook helper(s) checked")
        Warnings = @()
    }
}

function Get-StackValidationPlan {
    param(
        [Parameter(Mandatory = $true)][object] $ProjectProfile,
        [switch] $SkipTests,
        [switch] $BuiltinsOnly
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

    $toolingSettings = Join-Path $ProjectProfile.Root '.claude\settings.json'
    $customCheck = Join-Path $ProjectProfile.Root 'tools\check.ps1'
    if ((Test-Path -LiteralPath $toolingSettings -PathType Leaf) -or (Test-Path -LiteralPath $customCheck -PathType Leaf)) {
        [void] $plan.Add([pscustomobject]@{
                Name = 'tooling-layout'
                Action = { param([object] $ProjectProfile) Get-ToolingLayoutOutcome -ProjectProfile $ProjectProfile }
                ActionArguments = @{ ProjectProfile = $ProjectProfile }
            })
    }

    if ($BuiltinsOnly) { return @($plan) }

    if ($ProjectProfile.Stack -contains 'dotnet') {
        $projects = @($ProjectProfile.Files | Where-Object { $_.Extension -in @('.csproj', '.fsproj', '.vbproj') })
        foreach ($project in $projects) {
            $relative = Get-RelativeRepositoryPath -Path $project.FullName -Root $ProjectProfile.Root
            $buildName = "dotnet-build:$relative"
            [void] $plan.Add([pscustomobject]@{ Name = $buildName; Executable = 'dotnet'; Arguments = @('build', $relative, '--no-restore', '--nologo'); WorkingDirectory = '.' })
            try {
                $xml = Get-SafeXmlDocument -Path $project.FullName
                $isTest = $xml.SelectSingleNode('//*[local-name()="IsTestProject" and translate(normalize-space(text()),"TRUE","true")="true"]') -or
                    $xml.SelectSingleNode('//*[local-name()="PackageReference" and (@Include="Microsoft.NET.Test.Sdk" or @Include="TUnit")]')
                if ($isTest -and -not $SkipTests) {
                    [void] $plan.Add([pscustomobject]@{ Name = "dotnet-test:$relative"; Executable = 'dotnet'; Arguments = @('test', $relative, '--no-build', '--no-restore', '--nologo'); DependsOn = @($buildName); WorkingDirectory = '.' })
                }
            }
            catch { [void] $plan.Add((New-UnavailableStage -Name "dotnet-plan:$relative" -Reason 'Invalid project XML; fix the configuration stage first.')) }
        }
        if ($projects.Count -eq 0) { [void] $plan.Add((New-UnavailableStage -Name 'dotnet-plan' -Reason 'No .NET project files found. Add tools/check.ps1 for a solution-only layout.')) }
    }
    if ($ProjectProfile.Stack -contains 'node') {
        foreach ($package in $ProjectProfile.Files | Where-Object { $_.Name -eq 'package.json' }) {
            $relative = Get-RelativeRepositoryPath -Path $package.FullName -Root $ProjectProfile.Root
            $data = Get-NodePackageData -PackageFile $package
            if ($null -eq $data) { [void] $plan.Add((New-UnavailableStage -Name "node-plan:$relative" -Reason 'Invalid package.json.')); continue }
            $manager = Get-NodeManager -PackageFile $package -Root $ProjectProfile.Root
            if (-not $manager) { [void] $plan.Add((New-UnavailableStage -Name "node-plan:$relative" -Reason 'Ambiguous or unsupported package manager. Add tools/check.ps1.')); continue }
            $scripts = $data.PSObject.Properties['scripts']
            $workingDirectory = Get-RelativeRepositoryPath -Path $package.DirectoryName -Root $ProjectProfile.Root
            $scriptCount = 0
            foreach ($task in @('lint', 'build', 'test')) {
                if ($task -eq 'test' -and $SkipTests) { continue }
                $entry = if ($null -ne $scripts -and $null -ne $scripts.Value) { $scripts.Value.PSObject.Properties[$task] } else { $null }
                if ($null -ne $entry -and -not [string]::IsNullOrWhiteSpace([string]$entry.Value)) {
                    [void] $plan.Add([pscustomobject]@{ Name = "node-${task}:$relative"; Executable = $manager; Arguments = @('run', $task); WorkingDirectory = $workingDirectory })
                    $scriptCount++
                }
                elseif ($task -eq 'test') { [void] $plan.Add((New-UnavailableStage -Name "node-test:$relative" -Reason "${relative}: no test script; define one or own the check in tools/check.ps1.")) }
            }
            if ($scriptCount -eq 0 -and $SkipTests) { [void] $plan.Add((New-UnavailableStage -Name "node-plan:$relative" -Reason "${relative}: no lint or build script.")) }
        }
    }
    foreach ($stack in @('python', 'rust', 'go', 'java')) {
        if ($ProjectProfile.Stack -contains $stack) {
            [void] $plan.Add((New-UnavailableStage -Name "$stack-plan" -Reason "Detected $stack. Define its environment and offline validation in tools/check.ps1; see docs/tooling.md."))
        }
    }
    $sourceStacks = @{ '.cs' = 'dotnet'; '.fs' = 'dotnet'; '.vb' = 'dotnet'; '.js' = 'node'; '.ts' = 'node'; '.tsx' = 'node'; '.jsx' = 'node'; '.py' = 'python'; '.rs' = 'rust'; '.go' = 'go'; '.java' = 'java' }
    $unconfigured = @(
        foreach ($file in $ProjectProfile.Files) {
            $relative = Get-RelativeRepositoryPath -Path $file.FullName -Root $ProjectProfile.Root
            if ($relative -like 'src/*' -and $sourceStacks.ContainsKey($file.Extension) -and $sourceStacks[$file.Extension] -notin $ProjectProfile.Stack) { $sourceStacks[$file.Extension] }
        }
    ) | Sort-Object -Unique
    foreach ($stack in $unconfigured) { [void] $plan.Add((New-UnavailableStage -Name "$stack-manifest" -Reason "Source under src/ needs a $stack manifest or tools/check.ps1.")) }

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

function Get-NodeManager {
    param([System.IO.FileInfo] $PackageFile, [string] $Root)
    $directory = $PackageFile.Directory
    while ($null -ne $directory -and (Test-IsRepositoryPath -Path $directory.FullName -Root $Root)) {
        $manifest = Join-Path $directory.FullName 'package.json'
        if (Test-Path -LiteralPath $manifest -PathType Leaf) {
            $item = Get-Item -LiteralPath $manifest -Force
            if (-not (Test-IsSafeRepositoryFile -File $item -Root $Root)) { return $null }
            $data = Get-NodePackageData -PackageFile $item
            $field = if ($data) { $data.PSObject.Properties['packageManager'] } else { $null }
            if ($field) {
                if ($field.Value -match '^(npm|pnpm|yarn)@') { return $matches[1] }
                return $null
            }
        }
        $managers = @(
            if (Test-Path -LiteralPath (Join-Path $directory.FullName 'package-lock.json')) { 'npm' }
            if (Test-Path -LiteralPath (Join-Path $directory.FullName 'pnpm-lock.yaml')) { 'pnpm' }
            if (Test-Path -LiteralPath (Join-Path $directory.FullName 'yarn.lock')) { 'yarn' }
        )
        if ($managers.Count -gt 1) { return $null }
        if ($managers.Count -eq 1) { return $managers[0] }
        $directory = $directory.Parent
    }
    return 'npm'
}

function Get-ValidationPlan {
    param(
        [Parameter(Mandatory = $true)][object] $ProjectProfile,
        [switch] $SkipTests
    )

    # The project gate replaces inferred product stages while template checks remain.
    $checkScript = Join-Path $ProjectProfile.Root 'tools\check.ps1'
    if (Test-Path -LiteralPath $checkScript -PathType Leaf) {
        $file = Get-Item -LiteralPath $checkScript -Force
        if (-not (Test-IsSafeRepositoryFile -File $file -Root $ProjectProfile.Root)) { throw 'tools/check.ps1 must be a regular repository file.' }
        return @(Get-StackValidationPlan -ProjectProfile $ProjectProfile -SkipTests:$SkipTests -BuiltinsOnly) + @([pscustomobject]@{
                Name = 'project-check'
                Executable = Join-Path $PSHOME 'pwsh.exe'
                Arguments = @('-NoProfile', '-File', $checkScript) + $(if ($SkipTests) { @('-SkipTests') } else { @() })
                # A repository-owned gate speaks this command's exit codes: 0 pass,
                # 1 failure, 2 incomplete.
                ExitCodeContract = 'dev'
            })
    }

    return @(Get-StackValidationPlan -ProjectProfile $ProjectProfile -SkipTests:$SkipTests)
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
                foreach ($key in @('Executable', 'Arguments', 'WorkingDirectory', 'DependsOn', 'Reason')) {
                    $property = $stage.PSObject.Properties[$key]
                    if ($property) { $entry[$key] = $property.Value }
                }
                [pscustomobject]$entry
            }
        )
        Notes = @('Preview only. verify -Stage <name> includes dependencies and always reports incomplete.', 'Dependencies must already be installed/restored; project scripts must terminate and avoid live services.')
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

    # A stage may name its runner by a repository-relative path, such as a build
    # wrapper checked in beside the manifest. Resolve it against the repository root
    # rather than the working directory the command happened to start in.
    $executable = [string] $Stage.Executable
    if (-not [System.IO.Path]::IsPathRooted($executable) -and ($executable.Contains('/') -or $executable.Contains('\'))) {
        $rootedExecutable = Join-Path $Root $executable
        if (Test-Path -LiteralPath $rootedExecutable -PathType Leaf) { $executable = $rootedExecutable }
    }

    $command = Get-FirstCommand -Names @($executable)
    if ($null -eq $command -and -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        $missingExecutable = "Missing executable: $($Stage.Executable)"
        return [pscustomobject]@{ Name = $Stage.Name; Status = 'unavailable'; ExitCode = $null; Summary = @($missingExecutable); Warnings = @($missingExecutable); AdvisoryWarnings = $false }
    }

    if ($null -ne $command) { $executable = $command.Source }
    $workingDirectory = $Root
    $directoryProperty = $Stage.PSObject.Properties['WorkingDirectory']
    if ($directoryProperty -and $directoryProperty.Value) {
        $workingDirectory = [System.IO.Path]::GetFullPath((Join-Path $Root $directoryProperty.Value))
        if (-not (Test-IsRepositoryPath -Path $workingDirectory -Root $Root)) { throw 'Stage working directory must stay in the repository.' }
    }
    try {
        $timeout = 300
        $timeoutProperty = $Stage.PSObject.Properties['TimeoutSeconds']
        if ($timeoutProperty) { $timeout = [int]$timeoutProperty.Value }
        $native = Invoke-BoundedProcess -Executable $executable -Arguments $Stage.Arguments -WorkingDirectory $workingDirectory -TimeoutSeconds $timeout
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
    if ($status -eq 'pass') {
        $summary = @($lines | Where-Object { $_ -match '(?i)(check passed|tests passed|build succeeded|test run successful)' } | Select-Object -Last 3)
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
    $env:COREPACK_ENABLE_NETWORK = '0'
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
        $selected = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($name in $Stage) {
            if ($name -notin $plan.Name) { throw "Unknown stage '$name'. Run dev.ps1 plan for valid names." }
            [void]$selected.Add($name)
        }
        do {
            $added = $false
            foreach ($item in $plan | Where-Object { $selected.Contains($_.Name) }) {
                $dependencies = $item.PSObject.Properties['DependsOn']
                if ($dependencies) { foreach ($dependency in $dependencies.Value) { if ($selected.Add($dependency)) { $added = $true } } }
            }
        } while ($added)
        $plan = @($plan | Where-Object { $selected.Contains($_.Name) })
    }
    if ($plan.Count -eq 0) {
        return [pscustomobject]@{ Status = 'unavailable'; Stages = @(); Warnings = @('No validation command could be inferred. Add a project check script or supported build metadata.') }
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
        $outcomes = @{}
        foreach ($item in $plan) {
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $dependencies = $item.PSObject.Properties['DependsOn']
            $blocked = @()
            if ($dependencies) { $blocked = @($dependencies.Value | Where-Object { -not $outcomes.ContainsKey($_) -or $outcomes[$_].Status -ne 'pass' }) }
            $result = if ($blocked.Count -gt 0) {
                Invoke-ValidationStage -Stage (New-UnavailableStage -Name $item.Name -Reason "Prerequisite did not pass: $($blocked -join ', ')") -Root $projectProfile.Root
            }
            else { Invoke-ValidationStage -Stage $item -Root $projectProfile.Root }
            $result | Add-Member -NotePropertyName DurationMs -NotePropertyValue $watch.ElapsedMilliseconds
            $outcomes[$item.Name] = $result
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
    if ($Stage) { [void] $warnings.Add('Only selected stages and their prerequisites ran; run verify for the full gate.') }

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
