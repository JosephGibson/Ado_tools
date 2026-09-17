# Pure helpers shared by opt-in checks and offline synthetic tests. No network or configuration.
function Get-AdoLivePropertyValue {
    param([AllowNull()][object] $Object, [string] $Name)
    if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $null
}

function Get-AdoLiveAttemptTuple {
    param([AllowNull()][object] $Run)
    $pipeline = Get-AdoLivePropertyValue $Run 'pipelineReference'
    foreach ($name in @('stageReference', 'phaseReference', 'jobReference')) {
        $reference = Get-AdoLivePropertyValue $pipeline $name
        $attempt = 0
        if ([int]::TryParse([string] (Get-AdoLivePropertyValue $reference 'attempt'), [ref] $attempt) -and $attempt -gt 0) { $attempt }
        else { 0 }
    }
}

function Test-AdoLiveReattemptEvidence {
    param([AllowEmptyCollection()][object[]] $Runs)
    $seen = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($run in $Runs) {
        $pipeline = Get-AdoLivePropertyValue $run 'pipelineReference'
        $runId = 0
        if (-not [int]::TryParse([string] (Get-AdoLivePropertyValue $run 'id'), [ref] $runId) -or $runId -lt 1) { continue }
        $names = @(foreach ($level in @('stage', 'phase', 'job')) {
                [string] (Get-AdoLivePropertyValue (Get-AdoLivePropertyValue $pipeline ($level + 'Reference')) ($level + 'Name'))
            })
        # An attempt counter without a job identity cannot establish that two runs are retries.
        if ($names.Count -ne 3 -or [string]::IsNullOrEmpty([string] $names[2])) { continue }
        $key = ConvertTo-Json -InputObject @($names) -Compress
        $attempts = @(Get-AdoLiveAttemptTuple $run)
        $tuple = $attempts -join ','
        if (-not $seen.ContainsKey($key)) {
            $seen[$key] = @{ Tuples = [System.Collections.Generic.HashSet[string]]::new();
                RunIds = [System.Collections.Generic.HashSet[int]]::new(); Retried = $false }
        }
        if (-not $seen[$key].RunIds.Add($runId)) { continue }
        [void] $seen[$key].Tuples.Add($tuple)
        $seen[$key].Retried = $seen[$key].Retried -or @($attempts | Where-Object { $_ -gt 1 }).Count -gt 0
        if ($seen[$key].Tuples.Count -gt 1 -and $seen[$key].Retried) { return $true }
    }
    return $false
}

function Test-AdoLiveRerunDetail {
    param([AllowNull()][object] $Detail)
    if ((Get-AdoLivePropertyValue $Detail 'resultGroupType') -ne 'rerun') { return $false }
    $children = @(Get-AdoLivePropertyValue $Detail 'subResults')
    if ($children.Count -lt 2) { return $false }
    $sequences = [System.Collections.Generic.HashSet[int]]::new()
    $outcomes = @()
    foreach ($child in $children) {
        $sequence = 0
        if (-not [int]::TryParse([string] (Get-AdoLivePropertyValue $child 'sequenceId'), [ref] $sequence) -or
            -not $sequences.Add($sequence)) { return $false }
        $outcomes += [string] (Get-AdoLivePropertyValue $child 'outcome')
    }
    return $outcomes -contains 'Passed' -and @($outcomes | Where-Object { $_ -in @('Failed', 'Error', 'Timeout', 'Aborted') }).Count -gt 0
}

# Schema allowlists, not character filters: even an ASCII property name can contain work data.
# Unknown members are recorded under a fixed placeholder and never traversed. Dynamic bags are
# opaque at every depth, including arrays and customFields[].value.
function Get-AdoShapeMembers {
    param([string] $Schema)
    $members = @{}
    $scalars = switch ($Schema) {
        'Entity' { 'id name state isAutomated startedDate completedDate totalTests passedTests notApplicableTests unanalyzedTests incompleteTests outcome automatedTestName automatedTestStorage testCaseTitle resultGroupType durationInMs errorMessage stackTrace computerName failureType resolutionState comment priority url count buildNumber sourceBranch sourceVersion status result queueTime startTime finishTime uri revision path type order attempt identifier errorCount warningCount lineCount fileName size attachmentType sequenceId displayName asOf queryType queryResultType referenceName lastUpdatedDate defaultSuiteId' }
        'Reference' { 'id name url uniqueName displayName descriptor imageUrl type number projectId' }
        'Pipeline' { 'pipelineId' }
        'Stage' { 'attempt stageName' }
        'Phase' { 'attempt phaseName' }
        'Job' { 'attempt jobName' }
        'Statistic' { 'state outcome count' }
        'CustomField' { 'fieldName' }
        'Iteration' { 'id outcome errorMessage startedDate completedDate durationInMs' }
        'Parameter' { 'parameterName value iterationId actionPath' }
        'Action' { 'actionPath stepIdentifier outcome errorMessage' }
        'Issue' { 'type category message' }
        'FailingSince' { 'date' }
        'PreviousAttempt' { 'attempt recordId timelineId' }
        default { '' }
    }
    foreach ($name in $scalars.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)) { $members[$name] = 'Scalar' }
    switch ($Schema) {
        'Entity' {
            foreach ($name in @('value', 'records', 'subResults')) { $members[$name] = 'Entity' }
            foreach ($name in @('build', 'testRun', 'testCase', 'associatedBugs', 'owner', 'runBy', 'definition', 'repository',
                    'project', 'parentSuite', 'rootSuite', 'workItem', 'log', 'workItemTypes', 'defaultWorkItemType', 'workItems')) { $members[$name] = 'Reference' }
            $members.pipelineReference = 'Pipeline'; $members.runStatistics = 'Statistic'; $members.customFields = 'CustomField'
            $members.failingSince = 'FailingSince'; $members.iterationDetails = 'Iteration'
            $members.issues = 'Issue'; $members.previousAttempts = 'PreviousAttempt'
        }
        'Pipeline' { $members.stageReference = 'Stage'; $members.phaseReference = 'Phase'; $members.jobReference = 'Job' }
        'CustomField' { $members.value = 'Opaque' }
        'Iteration' { $members.parameters = 'Parameter'; $members.actionResults = 'Action' }
        'FailingSince' { $members.build = 'Reference'; $members.release = 'Reference' }
    }
    foreach ($name in @('_links', 'links', 'fields', 'workItemFields', 'properties', 'configurationValues')) { $members[$name] = 'Opaque' }
    return $members
}

function Get-AdoSafeJsonPath {
    param([string] $Path)
    if ($Path -notmatch '^\$(\.[A-Za-z_][A-Za-z0-9_]*|\[\d+\])*$' -or $Path.Length -gt 200) { return 'UNPRINTABLE' }
    $schema = 'Entity'
    foreach ($segment in [regex]::Matches($Path, '\.([A-Za-z_][A-Za-z0-9_]*)|\[\d+\]')) {
        if ($schema -in @('Opaque', 'Scalar')) { return 'UNPRINTABLE' }
        if ($segment.Value.StartsWith('[')) { continue }
        $members = Get-AdoShapeMembers $schema
        $name = $segment.Groups[1].Value
        if (-not $members.ContainsKey($name)) { return 'UNPRINTABLE' }
        $schema = $members[$name]
    }
    return $Path
}
