# Shared code of the three release scripts. Start-AdoToolkitRelease.ps1 prepares a version,
# Test-AdoToolkitRelease.ps1 checks it while the agent writes the prose, and
# Complete-AdoToolkitRelease.ps1 finishes the notes and writes the developer's handoff. Git is read
# with rev-parse, status, log and show only: nothing here commits, tags or pushes. A generated file
# is written beside its target, validated, then moved over it. Callers enable strict mode and
# terminating errors.

. (Join-Path $PSScriptRoot 'Package.Common.ps1')
# The discovery exclusions, the Markdown references and the plan of verify. Dot-sourcing
# tools/dev.ps1 sets its parameters in this scope, so a caller names nothing Command, Query, Path,
# Stage, Limit, Format, Install or SkipTests.
. (Join-Path $PSScriptRoot '../dev.ps1')

# Each declared version field: a file and the patterns that hold the version, {0} standing for it.
# Only these are replaced, never a bare version: from 0.11.0 on, the README, the guides and the
# release text name 0.11.0 on purpose, as the first version with Update-AdoToolkit.
$script:AdoReleaseVersionFields = @(
    @{ Path = 'Directory.Build.props'; Patterns = @('<VersionPrefix>{0}</VersionPrefix>') }
    @{ Path = 'README.md'; Patterns = @('**Version {0}**') }
    @{ Path = 'docs/guides/getting-started.md'; Patterns = @('AdoToolkit-{0}.zip', 'AdoToolkit-{0}-win-x64.zip') }
    @{ Path = 'tools/package/Install-AdoToolkit.ps1'; Patterns = @('AdoToolkit-{0}.zip', 'AdoToolkit-{0}-win-x64.zip') }
)
# The changelog groups of Keep a Changelog, in its order. An internal change reaches the notes only.
$script:AdoReleaseChangelogGroups = [ordered]@{ added = 'Added'; changed = 'Changed'; deprecated = 'Deprecated'; removed = 'Removed'; fixed = 'Fixed'; security = 'Security' }
$script:AdoReleaseFragmentKinds = @($script:AdoReleaseChangelogGroups.Keys) + @('internal')
$script:AdoReleaseFragmentFolder = 'docs/unreleased'
$script:AdoReleaseFragmentName = '^\d{8}-\d{6}-[a-z0-9]+(?:-[a-z0-9]+)*\.json$'
$script:AdoReleaseFragmentText = @('changelog', 'visible', 'contract', 'finding', 'regressionTest', 'testsChanged')
# What the agent still owes. The finalize script refuses while one remains.
$script:AdoReleaseTodo = 'release:todo'
$script:AdoReleaseCheckBegin = '<!-- release-check:begin -->'
$script:AdoReleaseCheckEnd = '<!-- release-check:end -->'
# A title or a summary goes into a single-quoted PowerShell string of the handoff.
$script:AdoReleaseQuotes = '[''"`' + (-join (0x2018..0x201F | ForEach-Object { [char] $_ })) + ']'

# The slim notes. The prep script fills the tokens; the agent replaces each release:todo marker, and
# the finalize script writes the Validation section between its two markers. The template lives
# here and not in a Markdown file, whose links would fail the documentation stage.
$script:AdoReleaseNotesTemplate = @'
# AdoToolkit @@VERSION@@ release notes

> <!-- release:todo: one line that says what this version is; the archive index repeats it after "@@VERSION@@ release notes:" -->

Prepared on @@DATE@@ from the branch `@@BRANCH@@`, which starts at `@@BASE@@`, the commit of AdoToolkit @@OLD@@ on `main`. <!-- release:todo: one or two sentences: what this version is, the live-validation status and the wall time of the release session -->

## What changed

<!-- release:todo: the final behavior by area, with one table per area at most -->
@@INTERNAL@@
### Visible changes for @@OLD@@ users

@@VISIBLE@@

### Public contract changes

@@CONTRACT@@

## Bug fixes

@@BUGS@@
@@TESTS@@
### Findings not fixed

| Finding | Reason |
| --- | --- |
| <!-- release:todo: a finding this version leaves unfixed, or delete this row --> | |
@@FINDINGS@@

### Known limitations

| Limitation | Notes |
| --- | --- |
| <!-- release:todo: a limitation this version adds, or delete this row --> | |
@@LIMITATIONS@@

## Work-PC Live checks

<!-- release:todo: the checks to run at work, most important first, with the evidence each one seeks -->

## Validation

<!-- release-check:begin -->
<!-- release-check:end -->
'@

# The handoff commands, with the placeholders the finalize script fills. Command 1 commits, tags,
# pushes both together, and then takes the pull request as far as it can with its final title:
# gh pr create where that works, otherwise the compare page, printed and opened with the title and
# the body encoded. The commit message and the pull-request title are one variable, so neither can
# drift from the other. The block after the push runs only when the push succeeded. Command 2 waits
# for the Verify check and then opens the pull request. The last one starts the next branch without
# tracking main, so that a bare git push cannot write to main.
$script:AdoReleasePublishCommand = '$title = ''<message>''; $body = ''Release of AdoToolkit <new>, published from tag v<new>. Changes: CHANGELOG.md and docs/release-<new>.md. Merge with Squash and merge, keeping this title.''; git add --all && git commit -m $title && git tag -a v<new> -m ''AdoToolkit <new>'' && git push --atomic <remote> <branch> v<new> && & { $created = $false; if (Get-Command gh -ErrorAction Ignore) { gh pr create --base main --head <branch> --title $title --body $body; $created = $LASTEXITCODE -eq 0 }; if (-not $created) { $repo = (git remote get-url <remote>) -replace ''^(?:\w+://)?(?:[^@/]+@)?([^:/]+)[:/]+'', ''https://$1/'' -replace ''(?:\.git)?/?$''; $url = "$repo/compare/main...<branch>?quick_pull=1&title=$([uri]::EscapeDataString($title))&body=$([uri]::EscapeDataString($body))"; $url; Start-Process $url } }'
$script:AdoReleaseWatchCommand = 'gh pr checks <branch> --watch --fail-fast && gh pr view <branch> --web'
$script:AdoReleaseNextCommand = 'git fetch origin && git switch --create <next> --no-track origin/main && git push --set-upstream origin <next>'

# The value of a JSON property, or $null when it is absent.
function Get-AdoReleaseProperty {
    param([AllowNull()][object] $Object, [Parameter(Mandatory = $true)][string] $Name)
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) { if ($Object.Contains($Name)) { return $Object[$Name] } else { return $null } }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function ConvertTo-AdoReleaseVersion {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text)
    if ($Text -notmatch '^\d+\.\d+\.\d+$') { throw "'$Text' is not a three-part version." }
    return [version] $Text
}

function Get-AdoRelativePath {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $FullPath)
    return [IO.Path]::GetRelativePath($Root, $FullPath).Replace('\', '/')
}

# Runs one of the Git commands that AGENTS.md allows, from the repository root.
function Invoke-AdoReleaseGit {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string[]] $Arguments)
    if ($Arguments[0] -notin @('rev-parse', 'status', 'log', 'show')) { throw "The release scripts do not run git $($Arguments[0])." }
    Push-Location -LiteralPath $Root
    try {
        $global:LASTEXITCODE = 0
        $lines = @(& git @Arguments 2>$null | ForEach-Object { [string] $_ })
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Lines = $lines }
    }
    finally { Pop-Location }
}

# The branch, its remote and the working-tree lines, from git status --short --branch. A branch
# with no upstream pushes to origin.
function Get-AdoReleaseBranch {
    param([Parameter(Mandatory = $true)][string] $Root)
    $status = Invoke-AdoReleaseGit -Root $Root -Arguments @('status', '--short', '--branch')
    if ($status.ExitCode -ne 0 -or $status.Lines.Count -eq 0) { throw 'git status did not describe the working tree.' }
    $first = $status.Lines[0]
    $changes = @($status.Lines | Select-Object -Skip 1)
    if ($first -cmatch '^## HEAD \(no branch\)') { return [pscustomobject]@{ Branch = $null; Remote = $null; Detached = $true; Changes = $changes } }
    if ($first -notmatch '^## (?:No commits yet on )?(?<branch>[^\s.][^\s]*?)(?:\.\.\.(?<upstream>\S+))?(?: \[[^\]]*\])?$') { throw "git status gave an unexpected branch line: $first" }
    $branch = $matches['branch']
    $remote = 'origin'
    if ($matches.ContainsKey('upstream') -and $matches['upstream']) { $remote = $matches['upstream'].Split('/')[0] }
    return [pscustomobject]@{ Branch = $branch; Remote = $remote; Detached = $false; Changes = $changes }
}

# Refuses a branch the handoff cannot use: a detached HEAD, main, or a name that would need quoting.
function Assert-AdoReleaseBranch {
    param([Parameter(Mandatory = $true)][object] $State)
    if ($State.Detached) { throw 'HEAD is detached: check out the release branch first.' }
    if ($State.Branch -ceq 'main') { throw 'The release is prepared on its own branch, never on main.' }
    foreach ($name in @($State.Branch, $State.Remote)) {
        if ($name -notmatch '^[A-Za-z0-9][A-Za-z0-9._/-]*$') { throw "The branch or remote name '$name' cannot go into the handoff commands." }
    }
}

function Test-AdoReleaseTag {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version)
    return (Invoke-AdoReleaseGit -Root $Root -Arguments @('rev-parse', '--verify', '--quiet', "refs/tags/v$Version")).ExitCode -eq 0
}

# The first commit in the history of HEAD whose subject starts with "AdoToolkit <old>:": the squash
# merge of the previous version, where the branch starts.
function Get-AdoReleaseBase {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Old)
    $log = Invoke-AdoReleaseGit -Root $Root -Arguments @('log', '--format=%H%x09%h%x09%s', 'HEAD')
    if ($log.ExitCode -ne 0) { throw 'git log could not read the history of HEAD.' }
    foreach ($line in $log.Lines) {
        $parts = $line.Split("`t", 3)
        if ($parts.Count -eq 3 -and $parts[2].StartsWith("AdoToolkit ${Old}:", [StringComparison]::Ordinal)) {
            return [pscustomobject]@{ Commit = $parts[0]; Short = $parts[1]; Subject = $parts[2] }
        }
    }
    throw "No commit in the history of HEAD has a subject that starts with 'AdoToolkit ${Old}:'."
}

# Whether GitHub already has the release: exists, absent, or unknown when gh is missing or fails.
function Get-AdoRemoteReleaseState {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version)
    if ($null -eq (Get-Command -Name gh -ErrorAction Ignore)) { return 'unknown' }
    Push-Location -LiteralPath $Root
    try {
        $global:LASTEXITCODE = 0
        $output = @(& gh release view "v$Version" --json tagName 2>&1 | ForEach-Object { [string] $_ })
        if ($LASTEXITCODE -eq 0) { return 'exists' }
        if (($output -join "`n") -match '(?i)release not found') { return 'absent' }
        return 'unknown'
    }
    finally { Pop-Location }
}

function Get-AdoDeclaredVersion {
    param([Parameter(Mandatory = $true)][string] $Root)
    $properties = Read-AdoPackageXml -Path (Join-Path $Root 'Directory.Build.props')
    $node = $properties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')
    $text = if ($null -ne $node) { $node.InnerText } else { '' }
    $null = ConvertTo-AdoReleaseVersion -Text $text
    return $text
}

# The versions whose notes are under docs/, newest first.
function Get-AdoLiveNotesVersion {
    param([Parameter(Mandatory = $true)][string] $Root)
    @(Get-ChildItem -LiteralPath (Join-Path $Root 'docs') -File -Filter 'release-*.md' |
            Where-Object { $_.Name -match '^release-(\d+\.\d+\.\d+)\.md$' } |
            ForEach-Object { $matches[1] } |
            Sort-Object { [version] $_ } -Descending)
}

# Where the notes of a version are now: docs/, docs/archive/, or nowhere.
function Get-AdoReleaseNotesPath {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version)
    foreach ($folder in @('docs', 'docs/archive')) {
        $path = Join-Path $Root "$folder/release-$Version.md"
        if (Test-Path -LiteralPath $path -PathType Leaf) { return $path }
    }
    return $null
}

function Get-AdoReleaseStatePath {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Name)
    return Resolve-AdoPackagePath -Path (Join-Path $Root "artifacts/release/$Name") -Root $Root
}

function Read-AdoReleaseState {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Name)
    $path = Get-AdoReleaseStatePath -Root $Root -Name $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    return ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable
}

# Checks text before it replaces a file of its kind.
function Test-AdoReleaseFileText {
    param([Parameter(Mandatory = $true)][string] $Path)
    switch -Regex ([IO.Path]::GetExtension($Path)) {
        '^\.(?:props|xml)$' { $null = Read-AdoPackageXml -Path $Path }
        '^\.json$' { $null = ConvertFrom-StrictJson -Text ([IO.File]::ReadAllText($Path)) }
        '^\.ps1$' {
            $errors = $null
            $null = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref] $null, [ref] $errors)
            if (@($errors).Count -gt 0) { throw "The new text of $([IO.Path]::GetFileName($Path)) does not parse." }
        }
    }
}

# Writes a temporary file beside the target, validates it as the target's kind, then replaces the
# target; the temporary file never outlives a failure. The target stays inside the repository and
# reaches it through no link.
function Write-AdoReleaseText {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text)
    $Path = Resolve-AdoPackagePath -Path $Path -Root $Root
    $directory = Split-Path -Parent $Path
    [void] [IO.Directory]::CreateDirectory($directory)
    $name = [IO.Path]::GetFileName($Path)
    # The temporary name keeps the extension, which decides how the text is checked.
    $temporary = Join-Path $directory (".$name." + [guid]::NewGuid().ToString('N') + [IO.Path]::GetExtension($name))
    try {
        [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($false))
        Test-AdoReleaseFileText -Path $temporary
        [IO.File]::Move($temporary, $Path, $true)
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}

function Write-AdoReleaseState {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Name, [Parameter(Mandatory = $true)][object] $State)
    Write-AdoReleaseText -Root $Root -Path (Get-AdoReleaseStatePath -Root $Root -Name $Name) -Text ((ConvertTo-Json -InputObject $State -Depth 8) + "`n")
}

function Get-AdoTextCount {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text, [Parameter(Mandatory = $true)][string] $Value)
    $count = 0
    $index = $Text.IndexOf($Value, [StringComparison]::Ordinal)
    while ($index -ge 0) { $count++; $index = $Text.IndexOf($Value, $index + $Value.Length, [StringComparison]::Ordinal) }
    return $count
}

# The new text of every declared version file. Replaced counts the old patterns, Holds the new ones
# afterwards: a file with neither declares no version, which fails the bump before anything is written.
function Get-AdoVersionBump {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Old, [Parameter(Mandatory = $true)][string] $New)
    foreach ($field in $script:AdoReleaseVersionFields) {
        $path = Join-Path $Root $field.Path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "The declared version file $($field.Path) is missing." }
        $text = [IO.File]::ReadAllText($path)
        $after = $text
        $replaced = 0
        foreach ($pattern in $field.Patterns) {
            $oldText = $pattern -f $Old
            $replaced += Get-AdoTextCount -Text $after -Value $oldText
            $after = $after.Replace($oldText, ($pattern -f $New))
        }
        $holds = 0
        $stale = 0
        foreach ($pattern in $field.Patterns) {
            $holds += Get-AdoTextCount -Text $after -Value ($pattern -f $New)
            $stale += Get-AdoTextCount -Text $after -Value ($pattern -f $Old)
        }
        [pscustomobject]@{ Path = $field.Path; FullPath = $path; Text = $text; After = $after; Replaced = $replaced; Holds = $holds; Stale = $stale }
    }
}

function Get-AdoFileSha256 {
    param([Parameter(Mandatory = $true)][string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# A fragment names a code file when the file is neither Markdown nor under docs/.
function Test-AdoCodeFile {
    param([Parameter(Mandatory = $true)][string] $Path)
    return -not ($Path -like '*.md' -or $Path -like 'docs/*')
}

# The problems of one fragment's data; none means it can be assembled.
function Test-AdoReleaseFragment {
    param([Parameter(Mandatory = $true)][AllowNull()][object] $Data)
    $problems = [Collections.Generic.List[string]]::new()
    if ($Data -isnot [System.Collections.IDictionary]) { return 'it is not a JSON object' }
    $known = @($script:AdoReleaseFragmentText) + @('kind', 'files', 'preFix')
    foreach ($key in $Data.Keys) { if ($key -cnotin $known) { $problems.Add("unknown property '$key'") } }
    $kind = Get-AdoReleaseProperty $Data 'kind'
    if ($kind -isnot [string] -or $kind -cnotin $script:AdoReleaseFragmentKinds) { $problems.Add("kind must be one of $($script:AdoReleaseFragmentKinds -join ', ')") }
    foreach ($name in $script:AdoReleaseFragmentText) {
        if (-not $Data.Contains($name)) { continue }
        $value = $Data[$name]
        if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value) -or $value -match '[\r\n]') { $problems.Add("$name must be one line of text") }
    }
    if (-not $Data.Contains('changelog')) { $problems.Add('changelog is required') }
    $files = @()
    if ($Data.Contains('files')) {
        $files = @($Data['files'])
        if ($Data['files'] -isnot [System.Collections.IList] -or $files.Count -eq 0 -or
            @($files | Where-Object { $_ -isnot [string] -or $_ -notmatch '^[A-Za-z0-9._-][^\\:*?"<>|]*$' -or $_ -match '(?:^|/)\.\.(?:/|$)' }).Count -gt 0) {
            $problems.Add('files must be a nonempty list of repository paths with forward slashes')
            $files = @()
        }
    }
    $preFix = Get-AdoReleaseProperty $Data 'preFix'
    if ($Data.Contains('preFix')) {
        $keys = @(if ($preFix -is [System.Collections.IDictionary]) { $preFix.Keys })
        $valid = $preFix -is [System.Collections.IDictionary] -and @($preFix.Values | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) -or $_ -match '[\r\n]' }).Count -eq 0 -and (
            (($keys.Count -eq 1) -and ($keys[0] -ceq 'none' -or $keys[0] -ceq 'notReproduced')) -or
            ($preFix.Contains('failure') -and $preFix.Contains('totals') -and @($keys | Where-Object { $_ -cnotin @('failure', 'totals', 'condition') }).Count -eq 0))
        if (-not $valid) { $problems.Add('preFix must be { failure, totals, condition? }, { none } or { notReproduced }, each a line of text') }
    }
    if ($kind -ceq 'fixed' -and -not $Data.Contains('finding')) { $problems.Add('a fixed fragment needs its finding') }
    if ($Data.Contains('finding')) {
        if (-not $Data.Contains('files')) { $problems.Add('a finding names its files') }
        if (@($files | Where-Object { Test-AdoCodeFile -Path $_ }).Count -gt 0 -and -not $Data.Contains('preFix')) {
            $problems.Add('a fix of code needs preFix: the failure observed before the fix, or { none } or { notReproduced } with the reason')
        }
    }
    if ($preFix -is [System.Collections.IDictionary] -and $preFix.Contains('failure') -and -not $Data.Contains('regressionTest')) {
        $problems.Add('a pre-fix failure needs the regressionTest that showed it')
    }
    return $problems.ToArray()
}

# The fragments under docs/unreleased/, in file-name order, each with its SHA-256 and problems.
function Get-AdoReleaseFragment {
    param([Parameter(Mandatory = $true)][string] $Root)
    $folder = Join-Path $Root $script:AdoReleaseFragmentFolder
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { return }
    $files = @(Get-ChildItem -LiteralPath $folder -File -Filter '*.json')
    [array]::Sort($files, [Comparison[object]] { param($left, $right) [string]::CompareOrdinal($left.Name, $right.Name) })
    foreach ($file in $files) {
        $problems = @()
        $data = $null
        if ($file.Name -cnotmatch $script:AdoReleaseFragmentName) { $problems += 'the name must be <yyyyMMdd-HHmmss>-<slug>.json, lowercase' }
        try {
            $text = [IO.File]::ReadAllText($file.FullName)
            $null = ConvertFrom-StrictJson -Text $text
            $data = ConvertFrom-Json -InputObject $text -AsHashtable
        }
        catch { $problems += "it is not valid JSON: $($_.Exception.Message)" }
        if ($null -ne $data -or $problems.Count -eq 0) { $problems += @(Test-AdoReleaseFragment -Data $data) }
        [pscustomobject]@{ Name = $file.Name; Path = $file.FullName; Sha256 = Get-AdoFileSha256 -Path $file.FullName; Data = $data; Problems = @($problems) }
    }
}

# How the fragments on disk differ from the journal. With -AllowMissing, a journaled fragment that is
# gone is no difference: the finalize script deletes them one by one and may be run again.
function Compare-AdoFragmentJournal {
    param([AllowEmptyCollection()][object[]] $Fragment = @(), [AllowEmptyCollection()][object[]] $Journal = @(), [switch] $AllowMissing)
    $recorded = @{}
    foreach ($entry in @($Journal | Where-Object { $null -ne $_ })) { $recorded[[string] $entry['Name']] = [string] $entry['Sha256'] }
    $present = @{}
    foreach ($item in $Fragment) {
        $present[$item.Name] = $true
        if (-not $recorded.ContainsKey($item.Name)) { "added after the journal: $($item.Name)" }
        elseif ($recorded[$item.Name] -ne $item.Sha256) { "changed after the journal: $($item.Name)" }
    }
    if (-not $AllowMissing) {
        foreach ($name in $recorded.Keys | Sort-Object) { if (-not $present.ContainsKey($name)) { "missing since the journal: $name" } }
    }
}

# Text for a Markdown table cell or a bullet.
function ConvertTo-AdoMarkdownCell {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text)
    return $Text.Replace('|', '\|')
}

function ConvertTo-AdoCodeSpan {
    param([Parameter(Mandatory = $true)][string] $Text)
    if ($Text.Contains('`')) { return ConvertTo-AdoMarkdownCell -Text $Text }
    return '`' + (ConvertTo-AdoMarkdownCell -Text $Text) + '`'
}

# "a", "a and b", "a, b and c".
function Join-AdoEnglishList {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Item)
    if ($Item.Count -le 1) { return $Item -join '' }
    return ($Item[0..($Item.Count - 2)] -join ', ') + ' and ' + $Item[-1]
}

function New-AdoChangelogSection {
    param([Parameter(Mandatory = $true)][string] $Version, [Parameter(Mandatory = $true)][string] $Date,
        [AllowEmptyCollection()][object[]] $Fragment = @())
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("## $Version - $Date")
    $lines.Add('')
    $entries = 0
    foreach ($kind in $script:AdoReleaseChangelogGroups.Keys) {
        $group = @($Fragment | Where-Object { $_.Data['kind'] -ceq $kind })
        if ($group.Count -eq 0) { continue }
        $lines.Add("### $($script:AdoReleaseChangelogGroups[$kind])")
        $lines.Add('')
        foreach ($item in $group) { $lines.Add('- ' + $item.Data['changelog']); $entries++ }
        $lines.Add('')
    }
    if ($entries -eq 0) {
        $lines.Add('### Changed')
        $lines.Add('')
        $lines.Add("- <!-- $($script:AdoReleaseTodo): one line that says what a user sees, under the group that fits -->")
        $lines.Add('')
    }
    $lines.Add("Details: [release notes](docs/release-$Version.md)")
    $lines.Add('')
    return $lines.ToArray()
}

# CHANGELOG.md with the section of the version first, in place of one an interrupted run left.
function Set-AdoChangelogSection {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text, [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string[]] $Section)
    $lines = [Collections.Generic.List[string]]::new([string[]] ($Text -split "`n"))
    $heading = '^## ' + [regex]::Escape($Version) + '(?:\s|$)'
    $start = -1
    for ($index = 0; $index -lt $lines.Count; $index++) { if ($lines[$index] -cmatch $heading) { $start = $index; break } }
    if ($start -ge 0) {
        $end = $start + 1
        while ($end -lt $lines.Count -and $lines[$end] -cnotmatch '^## ') { $end++ }
        $lines.RemoveRange($start, $end - $start)
    }
    $insert = $lines.Count
    for ($index = 0; $index -lt $lines.Count; $index++) { if ($lines[$index] -cmatch '^## ') { $insert = $index; break } }
    if ($insert -eq $lines.Count -and $lines.Count -gt 0 -and $lines[-1] -ne '') { $lines.Add(''); $insert++ }
    $lines.InsertRange($insert, $Section)
    return $lines -join "`n"
}

# The versions whose "Findings not fixed" or "Known limitations" an earlier notes file links. A row
# that links a single version hands over to it: the notes of 0.11.0 say that those of 0.10.5 link
# every earlier version, and archived notes keep links that no longer resolve, so their list is
# read here and linked again from where each file is now.
function Get-AdoLinkedNotesVersion {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][string] $Anchor, [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.HashSet[string]] $Seen)
    $path = Get-AdoReleaseNotesPath -Root $Root -Version $Version
    if ($null -eq $path) { return }
    $pattern = '\]\(\s*<?(?:[^()\s#>]*/)?release-(\d+\.\d+\.\d+)\.md#' + [regex]::Escape($Anchor) + '>?\s*\)'
    $linked = [Collections.Generic.List[string]]::new()
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($path), $pattern)) {
        $value = $match.Groups[1].Value
        if ($value -ne $Version -and -not $linked.Contains($value)) { $linked.Add($value) }
    }
    $linked
    if ($linked.Count -eq 1 -and $Seen.Add($linked[0])) { Get-AdoLinkedNotesVersion -Root $Root -Version $linked[0] -Anchor $Anchor -Seen $Seen }
}

# The carry-over rows of new notes under docs/: one link per earlier version, each pointing at where
# that file is once the preparation has run, under docs/ for <old> and under docs/archive/ otherwise.
function Get-AdoCarryOverRow {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Old)
    $rows = [ordered]@{}
    foreach ($anchor in @('findings-not-fixed', 'known-limitations')) {
        $seen = [Collections.Generic.HashSet[string]]::new()
        $versions = @(@($Old) + @(Get-AdoLinkedNotesVersion -Root $Root -Version $Old -Anchor $anchor -Seen $seen) |
                Select-Object -Unique | Where-Object {
                    $path = Get-AdoReleaseNotesPath -Root $Root -Version $_
                    $null -ne $path -and (Get-MarkdownAnchor -Line @(Get-Content -LiteralPath $path)).Contains($anchor)
                } | Sort-Object { [version] $_ } -Descending)
        $links = @($versions | ForEach-Object {
                $target = if ($_ -eq $Old) { "release-$_.md" } else { "archive/release-$_.md" }
                "[$_]($target#$anchor)"
            })
        $rows[$anchor] = if ($versions.Count -eq 0) { $null }
        elseif ($anchor -eq 'findings-not-fixed') { "| The findings of $(Join-AdoEnglishList -Item $versions) | Unchanged; see the $(Join-AdoEnglishList -Item $links) notes |" }
        else { "| Limits carried over | The $(Join-AdoEnglishList -Item $links) known limitations still apply |" }
    }
    return $rows
}

function Format-AdoPreFixCell {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary] $Data)
    $preFix = Get-AdoReleaseProperty $Data 'preFix'
    if ($null -eq $preFix) { return 'Not applicable: documentation only' }
    if ($preFix.Contains('none')) { return 'None needed: ' + $preFix['none'] }
    if ($preFix.Contains('notReproduced')) { return 'Not reproduced: ' + $preFix['notReproduced'] }
    $cell = "$($preFix['failure']); $($preFix['totals'])"
    if ($preFix.Contains('condition')) { $cell += "; only $($preFix['condition'])" }
    return $cell
}

# The notes of a new version from the template: the opening facts, the fragments' rows and the
# carry-over rows, with a release:todo marker wherever the agent owes prose.
function New-AdoReleaseNotes {
    param([Parameter(Mandatory = $true)][string] $Version, [Parameter(Mandatory = $true)][string] $Old,
        [Parameter(Mandatory = $true)][string] $Date, [Parameter(Mandatory = $true)][string] $Branch,
        [Parameter(Mandatory = $true)][string] $Base, [AllowEmptyCollection()][object[]] $Fragment = @(),
        [Parameter(Mandatory = $true)][System.Collections.IDictionary] $CarryOver)
    $bullets = {
        param([string] $Name)
        $items = @($Fragment | Where-Object { $_.Data.Contains($Name) } | ForEach-Object { '- ' + $_.Data[$Name] })
        if ($items.Count -eq 0) { return 'None.' }
        return $items -join "`n"
    }
    $internal = @($Fragment | Where-Object { $_.Data['kind'] -ceq 'internal' } | ForEach-Object { '- ' + $_.Data['changelog'] })
    $internalText = if ($internal.Count -gt 0) { "`n### Internal changes`n`n" + ($internal -join "`n") + "`n" } else { '' }
    $fixes = @($Fragment | Where-Object { $_.Data.Contains('finding') })
    $bugs = if ($fixes.Count -eq 0) { 'None: no change fragment of this version records a fix.' }
    else {
        $rows = @('| ID | Finding | Regression test | Files | Pre-fix failure |', '| --- | --- | --- | --- | --- |')
        for ($index = 0; $index -lt $fixes.Count; $index++) {
            $data = $fixes[$index].Data
            $test = if ($data.Contains('regressionTest')) { ConvertTo-AdoCodeSpan -Text $data['regressionTest'] } else { 'None' }
            $files = if ($data.Contains('files')) { (@($data['files']) | ForEach-Object { ConvertTo-AdoCodeSpan -Text $_ }) -join ', ' } else { '' }
            $rows += "| R-$($index + 1) | $(ConvertTo-AdoMarkdownCell -Text $data['finding']) | $test | $files | $(ConvertTo-AdoMarkdownCell -Text (Format-AdoPreFixCell -Data $data)) |"
        }
        $rows -join "`n"
    }
    $changedTests = @($Fragment | Where-Object { $_.Data.Contains('testsChanged') } | ForEach-Object { '- ' + $_.Data['testsChanged'] })
    $testsText = if ($changedTests.Count -gt 0) { "`n### Existing tests changed`n`n" + ($changedTests -join "`n") + "`n" } else { '' }
    $text = $script:AdoReleaseNotesTemplate.Replace("`r`n", "`n")
    foreach ($token in @(
            @('@@VERSION@@', $Version), @('@@OLD@@', $Old), @('@@DATE@@', $Date), @('@@BRANCH@@', $Branch), @('@@BASE@@', $Base),
            @('@@INTERNAL@@', $internalText), @('@@VISIBLE@@', (& $bullets 'visible')), @('@@CONTRACT@@', (& $bullets 'contract')),
            @('@@BUGS@@', $bugs), @('@@TESTS@@', $testsText))) {
        $text = $text.Replace($token[0], $token[1])
    }
    foreach ($anchor in @('findings-not-fixed', 'known-limitations')) {
        $token = if ($anchor -eq 'findings-not-fixed') { "@@FINDINGS@@`n" } else { "@@LIMITATIONS@@`n" }
        $row = $CarryOver[$anchor]
        $text = $text.Replace($token, $(if ($row) { "$row`n" } else { '' }))
    }
    return $text
}

# The one-line summary that follows the title of notes written from the template, or $null.
function Get-AdoNotesSummary {
    param([Parameter(Mandatory = $true)][string] $Path)
    $lines = @(Get-Content -LiteralPath $Path | Select-Object -First 5)
    if ($lines.Count -ge 3 -and $lines[0] -cmatch '^# ' -and $lines[1] -eq '' -and $lines[2] -cmatch '^> (\S.*)$') {
        $summary = $matches[1].Trim()
        if (($lines.Count -lt 4 -or $lines[3] -eq '') -and -not $summary.Contains($script:AdoReleaseTodo)) { return $summary }
    }
    return $null
}

# docs/archive/README.md with a row for archived notes, among the rows of release notes in
# version order. Notes without a summary line get a marker that the agent replaces.
function Add-AdoArchiveRow {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text, [Parameter(Mandatory = $true)][string] $Version,
        [AllowNull()][string] $Summary)
    $lines = [Collections.Generic.List[string]]::new([string[]] ($Text -split "`n"))
    $rowPattern = '^\| \[release-(\d+\.\d+\.\d+)\.md\]\(release-\1\.md\) \|'
    $after = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -notmatch $rowPattern) { continue }
        if ($matches[1] -eq $Version) { return $Text }
        if ([version] $matches[1] -lt [version] $Version) { $after = $index }
    }
    if ($after -lt 0) { throw 'docs/archive/README.md has no row of release notes to place the new row after.' }
    $description = if ($Summary) { $Summary } else { "<!-- $($script:AdoReleaseTodo): the one-line summary of these notes -->" }
    $lines.Insert($after + 1, "| [release-$Version.md](release-$Version.md) | $Version release notes: $description |")
    return $lines -join "`n"
}

# Every live Markdown file whose links or repository paths name a file that moves, with the new text.
# Archived Markdown is frozen and not checked, so it keeps its links. A dated file, the changelog or
# release notes, may name a path that has since gone, so only its links change.
function Get-AdoMovedFileEdit {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $From, [Parameter(Mandatory = $true)][string] $To,
        [hashtable] $Pending = @{})
    $source = [IO.Path]::GetFullPath($From)
    $target = [IO.Path]::GetFullPath($To)
    foreach ($file in @((Get-ProjectProfile -Root $Root).Files | Where-Object { $_.Extension -eq '.md' })) {
        $relative = Get-AdoRelativePath -Root $Root -FullPath $file.FullName
        if ($relative -match $script:FrozenDocumentationPattern -or [string]::Equals($file.FullName, $source, [StringComparison]::OrdinalIgnoreCase)) { continue }
        $dated = $relative -match $script:DatedDocumentationPattern
        $text = if ($Pending.ContainsKey($relative)) { $Pending[$relative] } else { [IO.File]::ReadAllText($file.FullName) }
        $lines = $text -split "`n"
        $changed = [Collections.Generic.List[int]]::new()
        foreach ($reference in @(Get-MarkdownReference -Line $lines)) {
            $index = $reference.Line - 1
            if ($reference.Kind -eq 'path') {
                if ($dated) { continue }
                $named = [IO.Path]::GetFullPath((Join-Path $Root $reference.Target.TrimEnd('/')))
                if (-not [string]::Equals($named, $source, [StringComparison]::OrdinalIgnoreCase)) { continue }
                $lines[$index] = $lines[$index].Replace('`' + $reference.Target + '`', '`' + (Get-AdoRelativePath -Root $Root -FullPath $target) + '`')
            }
            else {
                if ($reference.Target -match '^[A-Za-z][A-Za-z0-9+.-]*:') { continue }
                $path, $anchor = $reference.Target -split '#', 2
                if (-not $path) { continue }
                try { $resolved = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName ([uri]::UnescapeDataString($path)))) }
                catch { continue }
                if (-not [string]::Equals($resolved, $source, [StringComparison]::OrdinalIgnoreCase)) { continue }
                $link = [IO.Path]::GetRelativePath($file.DirectoryName, $target).Replace('\', '/')
                if ($anchor) { $link += "#$anchor" }
                $escaped = [regex]::Escape($reference.Target)
                $lines[$index] = [regex]::Replace($lines[$index], '(\]\(\s*)<?' + $escaped + '>?(?=[\s)])', { param($match) $match.Groups[1].Value + $link })
                $lines[$index] = [regex]::Replace($lines[$index], '^( {0,3}\[[^\]]+\]:\s*)<?' + $escaped + '>?(?=\s|$)', { param($match) $match.Groups[1].Value + $link })
            }
            if (-not $changed.Contains($reference.Line)) { $changed.Add($reference.Line) }
        }
        if ($changed.Count -gt 0) {
            [pscustomobject]@{ Path = $relative; FullPath = $file.FullName; After = $lines -join "`n"; Lines = @($changed) }
        }
    }
}

# Where a version still appears once the preparation is done. A declared field that holds it fails
# the preparation; every other line is listed for review, history apart from the rest.
function Get-AdoLeftover {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Old,
        [hashtable] $Pending = @{})
    $pattern = '(?<![\d.])' + [regex]::Escape($Old) + '(?!\d)'
    $history = [Collections.Generic.List[object]]::new()
    $other = [Collections.Generic.List[object]]::new()
    foreach ($file in @((Get-ProjectProfile -Root $Root).Files)) {
        $relative = Get-AdoRelativePath -Root $Root -FullPath $file.FullName
        if ($Pending.ContainsKey($relative)) { $text = $Pending[$relative] }
        elseif (Test-IsSearchableText -File $file) { $text = [IO.File]::ReadAllText($file.FullName) }
        else { continue }
        $lines = @()
        $number = 0
        foreach ($line in $text -split "`n") { $number++; if ($line -match $pattern) { $lines += $number } }
        if ($lines.Count -eq 0) { continue }
        $entry = [pscustomobject]@{ Path = $relative; Lines = @($lines | Select-Object -First 20) }
        if ($relative -ceq 'CHANGELOG.md' -or $relative -match '^docs/(?:release-[^/]+\.md$|archive/)') { $history.Add($entry) } else { $other.Add($entry) }
    }
    return [pscustomobject]@{ History = @($history); Other = @($other) }
}

# What README.md must hold: one "**Version X.Y.Z**" line, naming the declared version, and no link
# to release notes, which each entry of the changelog links.
function Get-AdoReadmeProblem {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text, [Parameter(Mandatory = $true)][string] $Version)
    $lines = @($Text -split "`r?`n" | Where-Object { $_ -match '\*\*Version [^*]*\*\*' })
    if ($lines.Count -ne 1) { "README.md has $($lines.Count) lines with **Version X.Y.Z**, not one." }
    elseif ($lines[0] -notmatch ('\*\*Version ' + [regex]::Escape($Version) + '\*\*')) { "The version line of README.md does not name $Version." }
    if ($Text -match '\]\(\s*<?(?:\./)?docs/(?:archive/)?release-[^)]*\)') { 'README.md links release notes, which the changelog links.' }
}

# The release:todo markers left in the three documents the agent writes.
function Get-AdoReleaseMarker {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version)
    foreach ($relative in @('CHANGELOG.md', "docs/release-$Version.md", 'docs/archive/README.md')) {
        $path = Join-Path $Root $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $number = 0
        foreach ($line in [IO.File]::ReadAllLines($path)) {
            $number++
            if ($line.Contains($script:AdoReleaseTodo)) { [pscustomobject]@{ Path = $relative; Line = $number } }
        }
    }
}

# Prepares a release: the version bump, the archive of the older notes, the changelog section and
# the notes assembled from the fragments. A run after a failure resumes from release-prep.json and
# skips each step whose end state already holds. -DryRun lists the edits and writes nothing.
function Invoke-AdoReleaseStart {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version,
        [string] $Date, [switch] $DryRun)
    $Root = [IO.Path]::GetFullPath($Root)
    $newVersion = ConvertTo-AdoReleaseVersion -Text $Version
    $warnings = [Collections.Generic.List[string]]::new()

    $branch = Get-AdoReleaseBranch -Root $Root
    Assert-AdoReleaseBranch -State $branch
    if (Test-AdoReleaseTag -Root $Root -Version $Version) { throw "The tag v$Version already exists: that version was released." }
    switch (Get-AdoRemoteReleaseState -Root $Root -Version $Version) {
        'exists' { throw "GitHub already has a release v$Version." }
        'unknown' { $warnings.Add("Whether GitHub has a release v$Version was not checked: gh is missing or could not answer.") }
    }

    $declared = Get-AdoDeclaredVersion -Root $Root
    $state = Read-AdoReleaseState -Root $Root -Name 'release-prep.json'
    if ($declared -eq $Version) {
        if ($null -eq $state -or $state['Version'] -ne $Version) {
            throw "VersionPrefix is already $Version, and artifacts/release/release-prep.json records no preparation of it. Restore the earlier version to prepare it again."
        }
        $mode = 'resume'
        if ($Date -and $Date -ne $state['Date']) { throw "The preparation of $Version started with the date $($state['Date'])." }
    }
    elseif ($newVersion -gt (ConvertTo-AdoReleaseVersion -Text $declared)) {
        $mode = 'fresh'
        if (-not $Date) { $Date = (Get-Date).ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture) }
        if ($Date -notmatch '^\d{4}-\d{2}-\d{2}$') { throw 'The date must be yyyy-MM-dd.' }
        if (Test-Path -LiteralPath (Join-Path $Root "docs/release-$Version.md")) { throw "docs/release-$Version.md already exists." }
        $older = @(Get-AdoLiveNotesVersion -Root $Root | Where-Object { [version] $_ -lt [version] $declared })
        if ($older.Count -gt 1) { throw "docs/ holds the notes of $($older -join ', ') beside those of ${declared}: only the current and the previous version belong there." }
        if (-not (Get-AdoReleaseNotesPath -Root $Root -Version $declared)) { throw "The notes of $declared, docs/release-$declared.md, are missing." }
        $base = Get-AdoReleaseBase -Root $Root -Old $declared
        $state = [ordered]@{
            Version = $Version; Old = $declared; Older = $(if ($older.Count -eq 1) { $older[0] } else { $null })
            Base = $base.Commit; BaseShort = $base.Short; Branch = $branch.Branch; Date = $Date
            Fragments = $null; AssemblyComplete = $false
        }
        if ([IO.File]::ReadAllText((Join-Path $Root 'CHANGELOG.md')) -cmatch ('(?m)^## ' + [regex]::Escape($Version) + '(?:\s|$)')) { throw "CHANGELOG.md already has a section for $Version." }
        if (-not $DryRun) { Write-AdoReleaseState -Root $Root -Name 'release-prep.json' -State $state }
    }
    else { throw "The new version must be higher than VersionPrefix, $declared." }

    $old = [string] $state['Old']
    $olderVersion = [string] $state['Older']
    $date = [string] $state['Date']
    $changed = [Collections.Generic.List[string]]::new()
    $pending = @{}

    # 1. The bump. Every file is checked before the first one is written.
    $bump = @(Get-AdoVersionBump -Root $Root -Old $old -New $Version)
    $missing = @($bump | Where-Object { $_.Replaced -eq 0 -and $_.Holds -eq 0 })
    if ($missing.Count -gt 0) { throw "No declared version field holds $old or ${Version}: $(($missing | ForEach-Object Path) -join ', ')." }
    foreach ($item in $bump | Where-Object { $_.Replaced -gt 0 }) {
        $pending[$item.Path] = $item.After
        if (-not $DryRun) { Write-AdoReleaseText -Root $Root -Path $item.FullPath -Text $item.After; $changed.Add($item.Path) }
    }

    # 2. The archive of the older notes, by the Archiving procedure of docs/AGENTS.md.
    $archive = $null
    if ($olderVersion) {
        $from = Join-Path $Root "docs/release-$olderVersion.md"
        $to = Join-Path $Root "docs/archive/release-$olderVersion.md"
        $moved = Test-Path -LiteralPath $to -PathType Leaf
        if ((Test-Path -LiteralPath $from) -and $moved) { throw "Both docs/release-$olderVersion.md and docs/archive/release-$olderVersion.md exist." }
        if (-not $moved -and -not (Test-Path -LiteralPath $from -PathType Leaf)) { throw "The notes of $olderVersion are in neither docs/ nor docs/archive/." }
        $summary = Get-AdoNotesSummary -Path $(if ($moved) { $to } else { $from })
        $edits = @(Get-AdoMovedFileEdit -Root $Root -From $from -To $to -Pending $pending)
        $index = Join-Path $Root 'docs/archive/README.md'
        $indexText = if ($pending.ContainsKey('docs/archive/README.md')) { $pending['docs/archive/README.md'] } else { [IO.File]::ReadAllText($index) }
        foreach ($edit in $edits | Where-Object { $_.Path -eq 'docs/archive/README.md' }) { $indexText = $edit.After }
        $indexAfter = Add-AdoArchiveRow -Text $indexText -Version $olderVersion -Summary $summary
        if (-not $DryRun) {
            if (-not $moved) {
                [IO.File]::Move((Resolve-AdoPackagePath -Path $from -Root $Root), (Resolve-AdoPackagePath -Path $to -Root $Root))
                $changed.Add("docs/release-$olderVersion.md"); $changed.Add("docs/archive/release-$olderVersion.md")
            }
            foreach ($edit in $edits | Where-Object { $_.Path -ne 'docs/archive/README.md' }) { Write-AdoReleaseText -Root $Root -Path $edit.FullPath -Text $edit.After; $changed.Add($edit.Path) }
            if ($indexAfter -cne [IO.File]::ReadAllText($index)) { Write-AdoReleaseText -Root $Root -Path $index -Text $indexAfter; $changed.Add('docs/archive/README.md') }
        }
        foreach ($edit in $edits) { $pending[$edit.Path] = $edit.After }
        $pending['docs/archive/README.md'] = $indexAfter
        $archive = [pscustomobject]@{
            From = "docs/release-$olderVersion.md"; To = "docs/archive/release-$olderVersion.md"; State = $(if ($moved) { 'already moved' } else { 'moved' })
            IndexRow = $(if ($summary) { 'from the summary line' } else { 'marker: the notes have no summary line' })
            Relinked = @($edits | ForEach-Object { [pscustomobject]@{ Path = $_.Path; Lines = $_.Lines } })
        }
    }

    # 3. The changelog section and the notes from the fragments, journaled before either is written.
    $fragments = @(Get-AdoReleaseFragment -Root $Root)
    $invalid = @($fragments | Where-Object { $_.Problems.Count -gt 0 })
    if ($invalid.Count -gt 0) { throw ('Invalid change fragments: ' + (($invalid | ForEach-Object { "$($_.Name): $($_.Problems -join '; ')" }) -join ' | ')) }
    $journal = $state['Fragments']
    if ($null -ne $journal) {
        # Once the notes are assembled, a fragment that the finish deleted is no difference.
        $differences = @(Compare-AdoFragmentJournal -Fragment $fragments -Journal @($journal) -AllowMissing:([bool] $state['AssemblyComplete']))
        if ($differences.Count -gt 0) { throw ('The fragments differ from the journal in release-prep.json: ' + ($differences -join '; ')) }
    }
    $assembly = 'already assembled'
    if (-not $state['AssemblyComplete']) {
        $assembly = 'assembled'
        $section = New-AdoChangelogSection -Version $Version -Date $date -Fragment $fragments
        $changelogPath = Join-Path $Root 'CHANGELOG.md'
        $changelog = Set-AdoChangelogSection -Text ([IO.File]::ReadAllText($changelogPath)) -Version $Version -Section $section
        # In a dry run the older notes are still under docs/; the rows link them where they will be.
        $carryOver = Get-AdoCarryOverRow -Root $Root -Old $old
        $notes = New-AdoReleaseNotes -Version $Version -Old $old -Date $date -Branch ([string] $state['Branch']) -Base ([string] $state['BaseShort']) `
            -Fragment $fragments -CarryOver $carryOver
        if (-not $DryRun) {
            $state['Fragments'] = @($fragments | ForEach-Object { [ordered]@{ Name = $_.Name; Sha256 = $_.Sha256 } })
            Write-AdoReleaseState -Root $Root -Name 'release-prep.json' -State $state
            Write-AdoReleaseText -Root $Root -Path $changelogPath -Text $changelog
            Write-AdoReleaseText -Root $Root -Path (Join-Path $Root "docs/release-$Version.md") -Text $notes
            $state['AssemblyComplete'] = $true
            Write-AdoReleaseState -Root $Root -Name 'release-prep.json' -State $state
            $changed.Add('CHANGELOG.md'); $changed.Add("docs/release-$Version.md")
        }
        $pending['CHANGELOG.md'] = $changelog
        $pending["docs/release-$Version.md"] = $notes
    }

    # 4. Leftovers: a stale declared field fails; every other line is for review.
    # The end state on disk; in a dry run, the text the bump would write.
    $stale = @(foreach ($field in $script:AdoReleaseVersionFields) {
            $text = if ($DryRun -and $pending.ContainsKey($field.Path)) { $pending[$field.Path] } else { [IO.File]::ReadAllText((Join-Path $Root $field.Path)) }
            if (@($field.Patterns | Where-Object { $text.Contains($_ -f $old) }).Count -gt 0) { $field.Path }
        })
    if ($stale.Count -gt 0) { throw "A declared version field still holds ${old}: $($stale -join ', ')." }
    $leftover = Get-AdoLeftover -Root $Root -Old $old -Pending $(if ($DryRun) { $pending } else { @{} })
    $markers = if ($DryRun) { @() } else { @(Get-AdoReleaseMarker -Root $Root -Version $Version) }

    return [pscustomobject]@{
        Status = 'pass'
        WhatIf = [bool] $DryRun
        Mode = $mode
        Version = $Version
        Old = $old
        Older = $(if ($olderVersion) { $olderVersion } else { $null })
        Base = [string] $state['BaseShort']
        Branch = [string] $state['Branch']
        Date = $date
        Bump = @($bump | ForEach-Object { [pscustomobject]@{ Path = $_.Path; Replacements = $_.Replaced; State = $(if ($_.Replaced -gt 0) { 'bump' } else { 'already bumped' }) } })
        Archive = $archive
        Fragments = @($fragments | ForEach-Object Name)
        Assembly = $assembly
        Changed = @($changed | Select-Object -Unique)
        Markers = @($markers | ForEach-Object { "$($_.Path):$($_.Line)" })
        Leftovers = $leftover
        Warnings = @($warnings)
    }
}

# Runs a program with an argument array and returns its exit code and output. The child inherits
# this process's environment, ADOTOOLKIT_RELEASE_BUILD included.
function Invoke-AdoReleaseProcess {
    param([Parameter(Mandatory = $true)][string] $FilePath, [AllowEmptyCollection()][string[]] $ArgumentList = @(),
        [Parameter(Mandatory = $true)][string] $WorkingDirectory, [ValidateRange(1, 7200)][int] $TimeoutSeconds = 1800)
    $info = [Diagnostics.ProcessStartInfo]::new($FilePath)
    foreach ($argument in $ArgumentList) { $info.ArgumentList.Add($argument) }
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    $process = [Diagnostics.Process]::Start($info)
    try {
        $process.StandardInput.Close()
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill($true); throw "$([IO.Path]::GetFileName($FilePath)) timed out after $TimeoutSeconds seconds." }
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output.GetAwaiter().GetResult(); Errors = $errors.GetAwaiter().GetResult() }
    }
    finally {
        if (-not $process.HasExited) { $process.Kill($true) }
        $process.Dispose()
    }
}

# The last lines a failed child printed, for its step's detail.
function Get-AdoProcessTail {
    param([Parameter(Mandatory = $true)][object] $Result)
    return @((([string] $Result.Output) + "`n" + ([string] $Result.Errors)) -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 8)
}

# Runs a PowerShell script of this repository in a pwsh process of its own.
function Invoke-AdoReleaseScript {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Script, [AllowEmptyCollection()][string[]] $ArgumentList = @())
    return Invoke-AdoReleaseProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -WorkingDirectory $Root `
        -ArgumentList (@('-NoProfile', '-NonInteractive', '-File', (Join-Path $Root $Script)) + $ArgumentList)
}

# verify with the named stages, through -Command so that the names reach it as an array, and each
# stage's status read from its JSON: with -Stage, the exit code is 2 even when every stage passes.
function Invoke-AdoReleaseVerify {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string[]] $Stage)
    $names = ($Stage | ForEach-Object { ConvertTo-PowerShellLiteral -Text $_ }) -join ', '
    $command = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(`$false); & $(ConvertTo-PowerShellLiteral -Text (Join-Path $Root 'tools/dev.ps1')) verify -Stage @($names); exit `$LASTEXITCODE"
    $result = Invoke-AdoReleaseProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -WorkingDirectory $Root -ArgumentList @('-NoProfile', '-NonInteractive', '-Command', $command)
    $json = @(([string] $result.Output) -split "`r?`n" | Where-Object { $_.TrimStart().StartsWith('{') }) | Select-Object -Last 1
    if (-not $json) { return [pscustomobject]@{ Status = 'fail'; Stages = @(); Detail = @(Get-AdoProcessTail -Result $result) } }
    $report = $json | ConvertFrom-Json
    $stages = @(@(Get-AdoReleaseProperty $report 'Stages') | Where-Object { $null -ne $_ } | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Status = $_.Status; Summary = @($_.Summary) } })
    $missing = @($Stage | Where-Object { $_ -notin @($stages | ForEach-Object Name) })
    $status = if (@($stages | Where-Object Status -eq 'fail').Count -gt 0 -or $missing.Count -gt 0 -or $report.Status -eq 'fail') { 'fail' }
    elseif (@($stages | Where-Object Status -ne 'pass').Count -gt 0) { 'incomplete' }
    else { 'pass' }
    $detail = @($stages | Where-Object Status -ne 'pass' | ForEach-Object { "$($_.Name) $($_.Status): $(@($_.Summary | Select-Object -First 4) -join ' | ')" }) +
        @($missing | ForEach-Object { "$_ did not run" })
    return [pscustomobject]@{ Status = $status; Stages = $stages; Detail = @($detail) }
}

# The content and the membership of every discovered file but the three documents the agent writes
# while the gate runs and the fragments, which the journal covers: path and SHA-256 per file, and
# one SHA-256 over them all.
function Get-AdoContentSnapshot {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version)
    $excluded = @('CHANGELOG.md', "docs/release-$Version.md", 'docs/archive/README.md')
    $files = [Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($file in @(Get-RepositoryFiles -Root $Root)) {
        $relative = Get-AdoRelativePath -Root $Root -FullPath $file.FullName
        if ($relative -cin $excluded -or $relative -match '^docs/unreleased/[^/]+\.json$') { continue }
        $files[$relative] = Get-AdoFileSha256 -Path $file.FullName
    }
    $text = ($files.GetEnumerator() | ForEach-Object { "$($_.Key)`t$($_.Value)" }) -join "`n"
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
    $map = [ordered]@{}
    foreach ($entry in $files.GetEnumerator()) { $map[$entry.Key] = $entry.Value }
    return [pscustomobject]@{ Sha256 = $hash; Count = $files.Count; Files = $map }
}

function Compare-AdoContentSnapshot {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary] $Before, [Parameter(Mandatory = $true)][System.Collections.IDictionary] $After)
    foreach ($key in $After.Keys) {
        if (-not $Before.Contains($key)) { "added: $key" }
        elseif ($Before[$key] -ne $After[$key]) { "changed: $key" }
    }
    foreach ($key in $Before.Keys) { if (-not $After.Contains($key)) { "removed: $key" } }
}

# The PowerShell runtime that the setup action pins, which the portable release bundles.
function Get-AdoRuntimePin {
    param([Parameter(Mandatory = $true)][string] $Root)
    $text = [IO.File]::ReadAllText((Join-Path $Root '.github/actions/setup/action.yml'))
    $version = [regex]::Match($text, "(?m)^\s*POWERSHELL_VERSION:\s*'(7\.6\.\d+)'")
    $hash = [regex]::Match($text, "(?m)^\s*POWERSHELL_SHA256:\s*'([0-9a-fA-F]{64})'")
    if (-not $version.Success -or -not $hash.Success) { throw 'The setup action does not pin POWERSHELL_VERSION and POWERSHELL_SHA256.' }
    return [pscustomobject]@{ Version = $version.Groups[1].Value; Sha256 = $hash.Groups[1].Value.ToLowerInvariant() }
}

# The passed counts of each test suite, from the summaries of the stages that ran them.
function Get-AdoReleaseTestCount {
    param([AllowEmptyCollection()][object[]] $Stage = @())
    foreach ($line in @($Stage | ForEach-Object { @($_.Summary) })) {
        if ([string] $line -match '^(?<suite>Pester|Core tests \((?:en-US|fr-CA)\)|product Pester): (?<passed>\d+) passed') {
            $suite = if ($matches['suite'] -eq 'Pester') { 'tooling Pester' } else { $matches['suite'] }
            [pscustomobject]@{ Suite = $suite; Passed = [int] $matches['passed'] }
        }
    }
}

function Read-AdoZipEntryText {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Entry)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $item = $zip.GetEntry($Entry)
        if ($null -eq $item) { return $null }
        $reader = [IO.StreamReader]::new($item.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

# Checks a prepared release: verify without its documentation stage, the version, the package, the
# release assets and what no other script checks, under ADOTOOLKIT_RELEASE_BUILD=1 for every child.
# The content of the tree is hashed before the gate and after packaging: a difference means that
# something changed while it ran. The result goes to artifacts/release/release-check.json.
function Invoke-AdoReleaseCheck {
    param([Parameter(Mandatory = $true)][string] $Root)
    $Root = [IO.Path]::GetFullPath($Root)
    $state = Read-AdoReleaseState -Root $Root -Name 'release-prep.json'
    if ($null -eq $state -or -not $state['AssemblyComplete']) { throw 'No completed preparation: run Start-AdoToolkitRelease.ps1 first.' }
    $version = [string] $state['Version']
    if ((Get-AdoDeclaredVersion -Root $Root) -ne $version) { throw "VersionPrefix is not $version, the version that release-prep.json prepared." }
    # The finish deletes the journaled fragments, and the check may run again after it.
    $differences = @(Compare-AdoFragmentJournal -Fragment @(Get-AdoReleaseFragment -Root $Root) -Journal @($state['Fragments']) -AllowMissing)
    if ($differences.Count -gt 0) { throw ('The fragments differ from the journal in release-prep.json: ' + ($differences -join '; ')) }

    $previous = $env:ADOTOOLKIT_RELEASE_BUILD
    $env:ADOTOOLKIT_RELEASE_BUILD = '1'
    $steps = [Collections.Generic.List[object]]::new()
    $assets = @()
    $portable = 'not built locally'
    $stages = @()
    try {
        $before = Get-AdoContentSnapshot -Root $Root -Version $version

        $names = @((Get-ProjectValidationPlan -Root $Root).Stages | ForEach-Object Name | Where-Object { $_ -ne 'documentation' })
        $gate = Invoke-AdoReleaseStep -Steps $steps -Name 'gate' -Body { Invoke-AdoReleaseVerify -Root $Root -Stage $names }
        if ($null -ne $gate -and $null -ne $gate.PSObject.Properties['Stages']) { $stages = @($gate.Stages) }

        $null = Invoke-AdoReleaseStep -Steps $steps -Name 'version' -Body {
            $dotnet = Get-Command -Name dotnet -CommandType Application -ErrorAction Ignore | Select-Object -First 1
            if ($null -eq $dotnet) { return [pscustomobject]@{ Status = 'fail'; Detail = @('dotnet is not on PATH.') } }
            $result = Invoke-AdoReleaseProcess -FilePath $dotnet.Source -WorkingDirectory $Root `
                -ArgumentList @('msbuild', 'src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj', '-getProperty:Version', '-nologo')
            $built = ([string] $result.Output).Trim()
            if ($result.ExitCode -ne 0 -or $built -ne $version) { return [pscustomobject]@{ Status = 'fail'; Detail = @("MSBuild gives '$built', not $version.") } }
            [pscustomobject]@{ Status = 'pass'; Detail = @($built) }
        }

        # The verified build, packaged as the release workflow packages it.
        $null = Invoke-AdoReleaseStep -Steps $steps -Name 'package' -Body {
            $result = Invoke-AdoReleaseScript -Root $Root -Script 'tools/package/Publish-AdoToolkitPackage.ps1' -ArgumentList @('-NoBuild')
            if ($result.ExitCode -ne 0) { return [pscustomobject]@{ Status = 'fail'; Detail = @(Get-AdoProcessTail -Result $result) } }
            [pscustomobject]@{ Status = 'pass'; Detail = @("AdoToolkit/$version") }
        }

        $release = Invoke-AdoReleaseStep -Steps $steps -Name 'assets' -Body {
            $pin = Get-AdoRuntimePin -Root $Root
            $runtime = Join-Path $Root "artifacts/runtime-download/PowerShell-$($pin.Version)-win-x64.zip"
            $hashes = Join-Path $Root 'artifacts/runtime-download/hashes.sha256'
            $withRuntime = (Test-Path -LiteralPath $runtime -PathType Leaf) -and (Test-Path -LiteralPath $hashes -PathType Leaf)
            $arguments = if ($withRuntime) { @('-PowerShellArchivePath', $runtime, '-PowerShellChecksumPath', $hashes, '-PowerShellVersion', $pin.Version) } else { @() }
            $result = Invoke-AdoReleaseScript -Root $Root -Script 'tools/package/New-AdoToolkitRelease.ps1' -ArgumentList $arguments
            if ($result.ExitCode -ne 0) { return [pscustomobject]@{ Status = 'fail'; Detail = @(Get-AdoProcessTail -Result $result) } }
            $output = Join-Path $Root 'artifacts/release'
            $files = @("AdoToolkit-$version.zip", "AdoToolkit-$version.zip.sha256", 'Install-AdoToolkit.ps1')
            if ($withRuntime) { $files = @("AdoToolkit-$version-win-x64.zip", "AdoToolkit-$version-win-x64.zip.sha256") + $files }
            $built = @($files | ForEach-Object {
                    $asset = Join-Path $output $_
                    [pscustomobject]@{ Name = $_; Bytes = $(if (Test-Path -LiteralPath $asset -PathType Leaf) { (Get-Item -LiteralPath $asset).Length } else { $null }) }
                })
            $smoke = 'not built locally'
            $detail = @()
            if ($withRuntime) {
                $archive = Join-Path $output "AdoToolkit-$version-win-x64.zip"
                $launch = Invoke-AdoReleaseScript -Root $Root -Script 'tools/package/Test-AdoToolkitPortable.ps1' `
                    -ArgumentList @('-ArchivePath', $archive, '-ModuleVersion', $version, '-PowerShellVersion', $pin.Version)
                if ($launch.ExitCode -ne 0) { return [pscustomobject]@{ Status = 'fail'; Detail = @(Get-AdoProcessTail -Result $launch) } }
                # New-AdoPortableArchive checks the runtime against its published hash; the pin of the
                # setup action, which CI bundles, is checked nowhere else.
                $bundle = Read-AdoZipEntryText -Path $archive -Entry 'bundle.json' | ConvertFrom-Json
                if ($bundle.PowerShellSha256 -ne $pin.Sha256) { return [pscustomobject]@{ Status = 'fail'; Detail = @('bundle.json names another PowerShell than the setup action pins.') } }
                $smoke = "passed: AdoToolkit $version, PowerShell $($pin.Version), Windows x64"
            }
            else { $detail += "The portable asset was not built: artifacts/runtime-download/ holds no PowerShell-$($pin.Version)-win-x64.zip with its hashes.sha256." }
            $install = Invoke-AdoReleaseScript -Root $Root -Script 'tools/package/Install-AdoToolkit.ps1' -ArgumentList @('-Path', (Join-Path $output "AdoToolkit-$version.zip"), '-WhatIf')
            if ($install.ExitCode -ne 0) { return [pscustomobject]@{ Status = 'fail'; Detail = @('Install-AdoToolkit.ps1 -WhatIf refused the module zip.') + @(Get-AdoProcessTail -Result $install) } }
            [pscustomobject]@{ Status = $(if ($withRuntime) { 'pass' } else { 'incomplete' }); Detail = $detail; Assets = $built; Portable = $smoke }
        }
        if ($null -ne $release -and $null -ne $release.PSObject.Properties['Assets']) { $assets = @($release.Assets); $portable = $release.Portable }

        $after = Get-AdoContentSnapshot -Root $Root -Version $version
        $drift = @(Compare-AdoContentSnapshot -Before $before.Files -After $after.Files)
        $steps.Add([pscustomobject]@{ Name = 'content'; Status = $(if ($drift.Count -eq 0) { 'pass' } else { 'fail' }); Detail = @($drift | Select-Object -First 20) })
    }
    finally {
        if ($null -eq $previous) { Remove-Item -Path Env:ADOTOOLKIT_RELEASE_BUILD -ErrorAction Ignore } else { $env:ADOTOOLKIT_RELEASE_BUILD = $previous }
    }

    $gh = @((Get-ProjectDiagnostics -Root $Root).Tools | Where-Object Name -eq 'gh') | Select-Object -First 1
    $pins = Import-PowerShellDataFile -LiteralPath (Join-Path $Root 'tools/BuildModules.psd1')
    $modules = [ordered]@{}
    foreach ($name in $pins.Keys | Sort-Object) { $modules[$name] = $pins[$name] }
    $status = if (@($steps | Where-Object Status -in @('fail', 'skipped')).Count -gt 0) { 'fail' }
    elseif (@($steps | Where-Object Status -ne 'pass').Count -gt 0) { 'incomplete' }
    else { 'pass' }
    $result = [ordered]@{
        Status = $status
        Version = $version
        Steps = @($steps)
        Stages = @($stages | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Status = $_.Status } })
        Tests = @(Get-AdoReleaseTestCount -Stage $stages)
        Modules = $modules
        Assets = @($assets)
        Portable = $portable
        Gh = $(if ($null -ne $gh) { $gh.State } else { 'not listed' })
        ContentSha256 = $before.Sha256
        Files = $before.Count
    }
    $record = [ordered]@{}
    foreach ($key in $result.Keys) { $record[$key] = $result[$key] }
    $record['Content'] = $before.Files
    Write-AdoReleaseState -Root $Root -Name 'release-check.json' -State $record
    return [pscustomobject]$result
}

# Runs one step of the release check and records it; after a failed step, the rest are skipped.
# The body returns Status and Detail, and may add data of its own.
function Invoke-AdoReleaseStep {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[object]] $Steps,
        [Parameter(Mandatory = $true)][string] $Name, [Parameter(Mandatory = $true)][scriptblock] $Body)
    if (@($Steps | Where-Object Status -eq 'fail').Count -gt 0) {
        $Steps.Add([pscustomobject]@{ Name = $Name; Status = 'skipped'; Detail = @() })
        return $null
    }
    try { $outcome = & $Body }
    catch { $outcome = [pscustomobject]@{ Status = 'fail'; Detail = @($_.Exception.Message) } }
    $Steps.Add([pscustomobject]@{ Name = $Name; Status = [string] $outcome.Status; Detail = @($outcome.Detail) })
    return $outcome
}

# The Validation section of the notes, from release-check.json: the gate with its module pins, the
# test counts and the assets with their sizes. It adds no link, so the documentation stage counts
# the same before and after it is written.
function New-AdoValidationSection {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary] $Check)
    $version = $Check['Version']
    $modules = @($Check['Modules'].GetEnumerator() | Sort-Object Key | ForEach-Object { "$($_.Key) $($_.Value)" })
    $stages = @($Check['Stages'] | ForEach-Object { '`' + $_['Name'] + '`' })
    $tests = @($Check['Tests'] | ForEach-Object { "$($_['Suite']) $($_['Passed'])" })
    $assets = @($Check['Assets'] | Where-Object { $null -ne $_['Bytes'] } | ForEach-Object { '`' + $_['Name'] + '` ' + ([long] $_['Bytes']).ToString('N0', [cultureinfo]::InvariantCulture) + ' bytes' })
    $lines = @(
        $script:AdoReleaseCheckBegin
        ''
        '| Check | Result |'
        '| --- | --- |'
        "| ``verify``, every stage but ``documentation``, with ``ADOTOOLKIT_RELEASE_BUILD=1`` | Passed: $(Join-AdoEnglishList -Item $stages), with $(Join-AdoEnglishList -Item $modules) |"
        "| Tests that passed | $(Join-AdoEnglishList -Item $tests) |"
        '| The `documentation` stage and the changelog and README tests | Passed on these notes, before the handoff |'
        "| Version and package | MSBuild gives $version; ``AdoToolkit/$version`` packaged from the verified build |"
        "| Release assets | $(Join-AdoEnglishList -Item $assets) |"
        "| Portable launcher | $(ConvertTo-AdoMarkdownCell -Text ([string] $Check['Portable'])) |"
        "| ``Install-AdoToolkit.ps1 -WhatIf`` | Accepted the checksum and the layout of ``AdoToolkit-$version.zip`` |"
        '| Content | Every file the gate checked is unchanged, apart from the changelog, these notes and the archive index |'
        ''
        'CI builds its own assets: compare the published files with the `.sha256` files of the release.'
        ''
        $script:AdoReleaseCheckEnd
    )
    return $lines -join "`n"
}

# The notes with their Validation section replaced; writing it twice gives the same text.
function Set-AdoValidationSection {
    param([Parameter(Mandatory = $true)][string] $Text, [Parameter(Mandatory = $true)][string] $Section)
    $begin = $Text.IndexOf($script:AdoReleaseCheckBegin, [StringComparison]::Ordinal)
    $end = $Text.IndexOf($script:AdoReleaseCheckEnd, [StringComparison]::Ordinal)
    if ($begin -lt 0 -or $end -lt $begin) { throw 'The notes have lost the release-check markers of their Validation section.' }
    return $Text.Substring(0, $begin) + $Section + $Text.Substring($end + $script:AdoReleaseCheckEnd.Length)
}

# The README test of Release.Tests.ps1 and the changelog test of ReleaseWorkflow.Tests.ps1, with the
# pinned Pester 5 imported first: a bare Invoke-Pester may load Pester 6.
function Invoke-AdoReleaseDocumentTest {
    param([Parameter(Mandatory = $true)][string] $Root)
    $pester = @(Get-BuildModule -Name Pester)
    if ($pester.Count -eq 0) { return [pscustomobject]@{ Status = 'fail'; Detail = @('Pester 5 is not installed.') } }
    $program = @"
Set-StrictMode -Version 2.0
`$ErrorActionPreference = 'Stop'
`$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new(`$false)
Import-Module -Name $(ConvertTo-PowerShellLiteral -Text $pester[0].Path)
`$configuration = New-PesterConfiguration
`$configuration.Run.Path = @($(ConvertTo-PowerShellLiteral -Text (Join-Path $Root 'tools/tests/Release.Tests.ps1')), $(ConvertTo-PowerShellLiteral -Text (Join-Path $Root 'tools/tests/ReleaseWorkflow.Tests.ps1')))
`$configuration.Filter.FullName = @('*README version line*', '*has that CHANGELOG.md section for the module version*')
`$configuration.Run.PassThru = `$true
`$configuration.Run.Exit = `$false
`$configuration.Output.Verbosity = 'None'
`$result = Invoke-Pester -Configuration `$configuration
[Console]::Out.WriteLine((ConvertTo-Json -Compress -InputObject ([ordered]@{ Passed = @(`$result.Passed | ForEach-Object { [string] `$_.ExpandedPath }); Failed = @(`$result.Failed | ForEach-Object { [string] `$_.ExpandedPath }) })))
"@
    $result = Invoke-AdoReleaseProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -WorkingDirectory $Root `
        -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($program)))
    $json = @(([string] $result.Output) -split "`r?`n" | Where-Object { $_.TrimStart().StartsWith('{') }) | Select-Object -Last 1
    if (-not $json) { return [pscustomobject]@{ Status = 'fail'; Detail = @(Get-AdoProcessTail -Result $result) } }
    # The filter leaves the other tests of the two files not run, so only the passed and failed
    # counts speak: the README tests and the changelog test, at least one of each.
    $counts = $json | ConvertFrom-Json
    $passed = @($counts.Passed)
    if (@($counts.Failed).Count -gt 0 -or @($passed -like 'README version line.*').Count -eq 0 -or @($passed -like '*has that CHANGELOG.md section*').Count -eq 0) {
        return [pscustomobject]@{ Status = 'fail'; Detail = @("$($passed.Count) passed") + @($counts.Failed) }
    }
    return [pscustomobject]@{ Status = 'pass'; Detail = @("$($passed.Count) passed") }
}

# The handoff: the three command lines that the release skill used to fill by hand, and the steps
# that follow them. The title is the commit message and the pull-request title at once.
function New-AdoReleaseHandoff {
    param([Parameter(Mandatory = $true)][string] $Version, [Parameter(Mandatory = $true)][string] $Title,
        [Parameter(Mandatory = $true)][string] $Branch, [Parameter(Mandatory = $true)][string] $Remote, [switch] $NoGh)
    $fill = { param([string] $Template) $Template.Replace('<new>', $Version).Replace('<remote>', $Remote).Replace('<branch>', $Branch).Replace('<message>', $Title) }
    $publish = & $fill $script:AdoReleasePublishCommand
    $watch = & $fill $script:AdoReleaseWatchCommand
    $next = $script:AdoReleaseNextCommand
    $lines = @(
        "# Handoff: AdoToolkit $Version"
        ''
        "Branch ``$Branch``, pushed to ``$Remote``. The tag ``v$Version`` does not exist."
        ''
        "Command 1 stages every change, commits, tags ``v$Version`` and pushes the branch and the tag together, then opens the pull request with its title already set, or the prefilled compare page when ``gh pr create`` cannot. **Pushing the tag publishes the release at once, before any review, and from 0.11.0 on, users who run ``Update-AdoToolkit`` receive it at once.**"
        ''
        '```powershell'
        $publish
        '```'
        ''
    )
    if ($NoGh) {
        $lines += @('Without `gh` there is no command 2: watch the Verify check on the pull request page and merge from there.', '')
    }
    else {
        $lines += @('Command 2 waits for the Verify check of the pull request and opens it in a browser once the check passes. If it reports no check yet, run it again.', '', '```powershell', $watch, '```', '')
    }
    $lines += @(
        'Then:'
        ''
        "1. Merge with Squash and merge, keeping the title GitHub proposes: ``$Title (#<n>)``."
        '2. Watch the release workflow and compare its five assets with the `.sha256` files it publishes. If it does not publish, `docs/tooling.md` lists the recovery for each cause, under Release workflow.'
        '3. Run the work-PC live checks of the notes with the published package.'
        '4. Once the pull request has closed, start the next branch from the updated `main`, naming `<next>`, the version after this one:'
        ''
        '   ```powershell'
        "   $next"
        '   ```'
        ''
    )
    return [pscustomobject]@{ Publish = $publish; Watch = $(if ($NoGh) { $null } else { $watch }); Next = $next; Text = $lines -join "`n" }
}

# Finishes a checked release: refuses while prose is owed, the check did not pass or the tree changed
# since the gate; writes the Validation section; runs the documentation stage and the changelog and
# README tests; deletes the journaled fragments; and writes the handoff. A second run after an
# interruption completes it.
function Invoke-AdoReleaseComplete {
    param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Summary)
    $Root = [IO.Path]::GetFullPath($Root)
    if ([string]::IsNullOrWhiteSpace($Summary) -or $Summary -match '[\r\n]' -or $Summary -match $script:AdoReleaseQuotes) {
        throw 'The summary is one line of text with no quote: it goes into the commit message inside single quotes.'
    }
    $state = Read-AdoReleaseState -Root $Root -Name 'release-prep.json'
    if ($null -eq $state -or -not $state['AssemblyComplete']) { throw 'No completed preparation: run Start-AdoToolkitRelease.ps1 first.' }
    $version = [string] $state['Version']
    if ((Get-AdoDeclaredVersion -Root $Root) -ne $version) { throw "VersionPrefix is not $version, the version that release-prep.json prepared." }
    $branch = Get-AdoReleaseBranch -Root $Root
    Assert-AdoReleaseBranch -State $branch
    if (Test-AdoReleaseTag -Root $Root -Version $version) { throw "The tag v$version already exists: that version was released." }

    $markers = @(Get-AdoReleaseMarker -Root $Root -Version $version)
    if ($markers.Count -gt 0) { throw ('Prose is still owed at ' + (($markers | ForEach-Object { "$($_.Path):$($_.Line)" }) -join ', ') + '.') }
    $check = Read-AdoReleaseState -Root $Root -Name 'release-check.json'
    if ($null -eq $check) { throw 'artifacts/release/release-check.json is missing: run Test-AdoToolkitRelease.ps1 first.' }
    $portableOnly = $check['Status'] -eq 'incomplete' -and
        @($check['Steps'] | Where-Object { $_['Status'] -ne 'pass' -and -not ($_['Name'] -eq 'assets' -and $_['Status'] -eq 'incomplete') }).Count -eq 0
    if ($check['Version'] -ne $version -or ($check['Status'] -ne 'pass' -and -not $portableOnly)) {
        throw "The release check of $($check['Version']) ended $($check['Status']): run Test-AdoToolkitRelease.ps1 again."
    }
    $now = Get-AdoContentSnapshot -Root $Root -Version $version
    $drift = @(Compare-AdoContentSnapshot -Before $check['Content'] -After $now.Files)
    if ($drift.Count -gt 0) { throw ('The tree changed after the release check started, so it runs again: ' + (($drift | Select-Object -First 10) -join '; ')) }
    $differences = @(Compare-AdoFragmentJournal -Fragment @(Get-AdoReleaseFragment -Root $Root) -Journal @($state['Fragments']) -AllowMissing)
    if ($differences.Count -gt 0) { throw ('The fragments differ from the journal in release-prep.json: ' + ($differences -join '; ')) }

    $notesPath = Join-Path $Root "docs/release-$version.md"
    $notes = [IO.File]::ReadAllText($notesPath)
    $withSection = Set-AdoValidationSection -Text $notes -Section (New-AdoValidationSection -Check $check)
    if ($withSection -cne $notes) { Write-AdoReleaseText -Root $Root -Path $notesPath -Text $withSection }

    $documentation = Invoke-AdoReleaseVerify -Root $Root -Stage @('documentation')
    if ($documentation.Status -ne 'pass') { throw ('The documentation stage did not pass: ' + (@($documentation.Detail) -join ' | ')) }
    $tests = Invoke-AdoReleaseDocumentTest -Root $Root
    if ($tests.Status -ne 'pass') { throw ('The changelog and README tests did not pass: ' + (@($tests.Detail) -join ' | ')) }

    $deleted = 0
    foreach ($entry in @($state['Fragments'])) {
        $path = Resolve-AdoPackagePath -Path (Join-Path $Root "$($script:AdoReleaseFragmentFolder)/$($entry['Name'])") -Root $Root
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force; $deleted++ }
    }

    $title = "AdoToolkit ${version}: $Summary"
    $handoff = New-AdoReleaseHandoff -Version $version -Title $title -Branch $branch.Branch -Remote $branch.Remote -NoGh:($check['Gh'] -ne 'present')
    Write-AdoReleaseText -Root $Root -Path (Get-AdoReleaseStatePath -Root $Root -Name 'handoff.md') -Text $handoff.Text
    return [pscustomobject]@{
        Status = 'pass'
        Version = $version
        Title = $title
        Branch = $branch.Branch
        Remote = $branch.Remote
        Handoff = 'artifacts/release/handoff.md'
        Gate = @($check['Stages'] | ForEach-Object { "$($_['Name']) $($_['Status'])" })
        Tests = @($check['Tests'] | ForEach-Object { "$($_['Suite']) $($_['Passed'])" })
        Assets = @($check['Assets'] | ForEach-Object { "$($_['Name']) $($_['Bytes'])" })
        Portable = $check['Portable']
        Gh = $check['Gh']
        FragmentsDeleted = $deleted
        WorkingTree = @($branch.Changes)
        Warnings = @($(if ($portableOnly) { 'The portable asset was not built locally; CI builds and checks it.' }))
    }
}
