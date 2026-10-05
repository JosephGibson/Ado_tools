# Pure helpers shared by opt-in checks and offline synthetic tests. No network or configuration.
function Get-AdoLivePropertyValue {
    param([AllowNull()][object] $Object, [string] $Name)
    if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $null
}

# The checks, of those given, that have no verdict among the printed lines. A NOTE or SHAPE line
# is not a verdict.
function Get-AdoLivePendingCheck {
    param([Parameter(Mandatory = $true)][string[]] $Id, [AllowEmptyCollection()][string[]] $Line = @())
    $settled = @(foreach ($text in $Line) {
            $words = $text.Split(' ')
            if ($words.Count -gt 1 -and $words[0] -in @('PASS', 'FAIL', 'INCONCLUSIVE')) { $words[1] }
        })
    return @($Id | Where-Object { $settled -cnotcontains $_ })
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

# What the child elements of the shared-step references (compref) in one steps document are, as
# one word: None (no reference), NoChildren, RepeatsSharedSteps (the children equal the steps of
# the referenced work item), OtherSteps (they differ, so they are content that work item does
# not hold), or Unknown (the referenced document was not supplied). The toolkit skips reference
# children (V-01), which loses nothing only for the first two. Text is compared, never returned.
function Get-AdoLiveComprefEvidence {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlDocument] $Document,
        # Steps documents keyed by work item ID in invariant text.
        [System.Collections.IDictionary] $ReferencedSteps = @{}
    )
    $describe = {
        param([System.Xml.XmlNode] $Parent)
        @(foreach ($child in $Parent.SelectNodes("*[local-name()='step' or local-name()='compref']")) {
                if ($child.LocalName -eq 'compref') { 'compref:' + $child.GetAttribute('ref') }
                else { 'step:' + (@($child.SelectNodes("*[local-name()='parameterizedString']") | ForEach-Object { $_.InnerText }) -join [char] 0x1F) }
            }) -join [char] 0x1E
    }
    $verdicts = @(foreach ($reference in $Document.SelectNodes("//*[local-name()='compref']")) {
            if ($reference.SelectNodes('*').Count -eq 0) { 'NoChildren'; continue }
            $key = $reference.GetAttribute('ref')
            if (-not $ReferencedSteps.Contains($key) -or $null -eq $ReferencedSteps[$key].DocumentElement) { 'Unknown'; continue }
            if ([string]::Equals((& $describe $reference), (& $describe $ReferencedSteps[$key].DocumentElement), [StringComparison]::Ordinal)) { 'RepeatsSharedSteps' }
            else { 'OtherSteps' }
        })
    foreach ($verdict in @('OtherSteps', 'Unknown', 'RepeatsSharedSteps', 'NoChildren')) {
        if ($verdicts -contains $verdict) { return $verdict }
    }
    return 'None'
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

function Get-AdoLiveIdValue {
    param([AllowNull()][object] $Value)
    $id = 0
    if ([int]::TryParse([string] $Value, [System.Globalization.NumberStyles]::None, [cultureinfo]::InvariantCulture, [ref] $id) -and $id -gt 0) { return $id }
    return 0
}

# What one Test Case, read with its relations, shows of the shapes that Export-AdoTestCase
# -IncludeDetail reads (V-31): the kinds of its fields and the attributes of its relations.
# Returns counts and fixed words only. A problem is an observation that the toolkit would lose or
# misread; a missing optional value is not one.
function Get-AdoLiveTestCaseDetailEvidence {
    param([AllowNull()][object] $Item, [int] $TestCaseId)
    $problems = [System.Collections.Generic.List[string]]::new()
    $counts = [ordered]@{ DESCRIPTION = 0; TAGS = 0; AUTOMATED = 0; LINKS = 0; LINKS_WITH_NAME = 0; HYPERLINKS = 0; ATTACHMENTS = 0; ATTACHMENTS_WITH_SIZE = 0 }
    $fields = Get-AdoLivePropertyValue $Item 'fields'
    if ($null -eq $fields) {
        $problems.Add('TESTCASE_UNREADABLE')
        return [pscustomobject]@{ Problems = $problems.ToArray(); Counts = $counts }
    }
    foreach ($entry in @(
            @{ Name = 'System.Description'; Count = 'DESCRIPTION' }, @{ Name = 'System.Tags'; Count = 'TAGS' },
            @{ Name = 'Microsoft.VSTS.TCM.AutomatedTestName'; Count = 'AUTOMATED' },
            @{ Name = 'Microsoft.VSTS.TCM.AutomatedTestStorage'; Count = $null }, @{ Name = 'Microsoft.VSTS.TCM.AutomatedTestType'; Count = $null })) {
        $value = Get-AdoLivePropertyValue $fields $entry.Name
        if ($null -eq $value) { continue }
        if ($value -isnot [string]) { $problems.Add('FIELD_NOT_TEXT') }
        elseif ($null -ne $entry.Count -and -not [string]::IsNullOrWhiteSpace($value)) { $counts[$entry.Count] = 1 }
    }
    $author = Get-AdoLivePropertyValue $fields 'System.CreatedBy'
    if ($null -eq $author) { $problems.Add('CREATED_BY_MISSING') }
    elseif ($author -isnot [string] -and (Get-AdoLivePropertyValue $author 'displayName') -isnot [string]) { $problems.Add('CREATED_BY_SHAPE') }
    $created = Get-AdoLivePropertyValue $fields 'System.CreatedDate'
    $parsed = [datetimeoffset]::MinValue
    if ($null -eq $created) { $problems.Add('CREATED_DATE_MISSING') }
    elseif ($created -isnot [datetime] -and $created -isnot [datetimeoffset] -and
        -not [datetimeoffset]::TryParse([string] $created, [cultureinfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref] $parsed)) {
        $problems.Add('CREATED_DATE_SHAPE')
    }
    $links = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($relation in @(Get-AdoLivePropertyValue $Item 'relations')) {
        $rel = Get-AdoLivePropertyValue $relation 'rel'
        if ($rel -isnot [string] -or [string]::IsNullOrEmpty($rel)) { continue }
        $attributes = Get-AdoLivePropertyValue $relation 'attributes'
        $name = Get-AdoLivePropertyValue $attributes 'name'
        $named = $name -is [string] -and -not [string]::IsNullOrWhiteSpace($name)
        $url = Get-AdoLivePropertyValue $relation 'url'
        if ($rel -eq 'AttachedFile') {
            $counts.ATTACHMENTS++
            # The toolkit lists an attachment by its name and skips one without it.
            if (-not $named) { $problems.Add('ATTACHMENT_WITHOUT_NAME') }
            $size = Get-AdoLivePropertyValue $attributes 'resourceSize'
            if ($size -is [int] -or $size -is [long]) { $counts.ATTACHMENTS_WITH_SIZE++ }
        }
        elseif ($rel -eq 'Hyperlink') {
            $counts.HYPERLINKS++
            if ($url -isnot [string] -or [string]::IsNullOrWhiteSpace($url)) { $problems.Add('HYPERLINK_WITHOUT_URL') }
        }
        elseif ($rel -ne 'ArtifactLink' -and $url -is [string] -and $url -match '/_apis/wit/workItems/([0-9]+)$') {
            $linked = Get-AdoLiveIdValue $Matches[1]
            # The toolkit shows each link type to a work item once, and never the Test Case itself.
            if ($linked -eq 0 -or $linked -eq $TestCaseId -or -not $links.Add($rel + '|' + $linked.ToString([cultureinfo]::InvariantCulture))) { continue }
            $counts.LINKS++
            if ($named) { $counts.LINKS_WITH_NAME++ }
        }
    }
    return [pscustomobject]@{ Problems = $problems.ToArray(); Counts = $counts }
}

# What the answer of the test points query shows (V-32). A point is placed when it names the
# requested Test Case and gives its plan and suite, by reference or in the path of its url.
function Get-AdoLiveTestPointEvidence {
    param([AllowNull()][object] $Response, [int] $TestCaseId)
    $problems = [System.Collections.Generic.List[string]]::new()
    $counts = [ordered]@{ POINTS = 0; WITH_PLAN_REFERENCE = 0; WITH_SUITE_REFERENCE = 0; WITH_NAMES = 0; WITH_CONFIGURATION = 0; RUN = 0; NOT_RUN = 0; WITH_TESTER = 0 }
    $ids = [System.Collections.Generic.List[int]]::new()
    $property = if ($null -eq $Response) { $null } else { $Response.PSObject.Properties['points'] }
    if ($null -eq $property -or ($null -ne $property.Value -and $property.Value -isnot [array])) {
        $problems.Add('POINTS_ARRAY_MISSING')
        return [pscustomobject]@{ Problems = $problems.ToArray(); Counts = $counts; Ids = $ids.ToArray() }
    }
    foreach ($point in @($property.Value)) {
        $counts.POINTS++
        $id = Get-AdoLiveIdValue (Get-AdoLivePropertyValue $point 'id')
        if ($id -eq 0) { $problems.Add('POINT_WITHOUT_ID'); continue }
        $ids.Add($id)
        if ((Get-AdoLiveIdValue (Get-AdoLivePropertyValue (Get-AdoLivePropertyValue $point 'testCase') 'id')) -ne $TestCaseId) { $problems.Add('POINT_FOR_ANOTHER_CASE') }
        $plan = Get-AdoLivePropertyValue $point 'testPlan'
        $suite = Get-AdoLivePropertyValue $point 'suite'
        $planId = Get-AdoLiveIdValue (Get-AdoLivePropertyValue $plan 'id')
        $suiteId = Get-AdoLiveIdValue (Get-AdoLivePropertyValue $suite 'id')
        if ($planId -gt 0) { $counts.WITH_PLAN_REFERENCE++ }
        if ($suiteId -gt 0) { $counts.WITH_SUITE_REFERENCE++ }
        $inPath = ([string] (Get-AdoLivePropertyValue $point 'url')) -match '/Plans/[0-9]+/Suites/[0-9]+/Points/'
        if (($planId -eq 0 -or $suiteId -eq 0) -and -not $inPath) { $problems.Add('POINT_NOT_PLACED') }
        if ((Get-AdoLivePropertyValue $plan 'name') -is [string] -and (Get-AdoLivePropertyValue $suite 'name') -is [string]) { $counts.WITH_NAMES++ }
        if ((Get-AdoLivePropertyValue (Get-AdoLivePropertyValue $point 'configuration') 'name') -is [string]) { $counts.WITH_CONFIGURATION++ }
        if ($null -ne (Get-AdoLivePropertyValue $point 'assignedTo')) { $counts.WITH_TESTER++ }
        $outcome = Get-AdoLivePropertyValue $point 'outcome'
        if ($null -ne $outcome -and $outcome -isnot [string]) { $problems.Add('OUTCOME_NOT_TEXT') }
        # The toolkit reads these two values, and no value, as a point that was never run.
        elseif ([string]::IsNullOrWhiteSpace($outcome) -or $outcome -in @('Unspecified', 'None')) { $counts.NOT_RUN++ }
        else { $counts.RUN++ }
    }
    return [pscustomobject]@{ Problems = $problems.ToArray(); Counts = $counts; Ids = $ids.ToArray() }
}

# One point per page, at offsets 0 and 1: HONORED when each page holds one point and they differ.
function Get-AdoLivePagingEvidence {
    param([AllowEmptyCollection()][int[]] $First, [AllowEmptyCollection()][int[]] $Second)
    if ($First.Count -gt 1 -or $Second.Count -gt 1) { return 'TOP_IGNORED' }
    if ($First.Count -eq 0 -or $Second.Count -eq 0) { return 'PAGE_EMPTY' }
    if ($First[0] -eq $Second[0]) { return 'SKIP_IGNORED' }
    return 'HONORED'
}

function Format-AdoLiveCounts {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary] $Counts)
    return (@($Counts.GetEnumerator() | ForEach-Object { $_.Key + '=' + ([int] $_.Value).ToString([cultureinfo]::InvariantCulture) }) -join ' ')
}

# V-28: the routes of an area's resource locations, the answer to OPTIONS on the area, that a
# later version would use. An allowlist entry names a route by its resource name and, where one
# name has several routes, says whether its template has a run segment. Only the entry's own
# name, PRESENT or ABSENT and the versions as numbers are returned.
function Get-AdoLiveRouteEvidence {
    param([AllowEmptyCollection()][object[]] $Location = @(), [Parameter(Mandatory = $true)][object[]] $Allowlist)
    return @(foreach ($entry in $Allowlist) {
            $found = @($Location | Where-Object {
                    ([string] (Get-AdoLivePropertyValue $_ 'resourceName')) -ieq $entry.ResourceName -and
                    ($null -eq $entry.PerRun -or ((([string] (Get-AdoLivePropertyValue $_ 'routeTemplate')) -match '\{runId\}') -eq $entry.PerRun)) })
            $first = if ($found.Count -gt 0) { $found[0] } else { $null }
            [pscustomobject]@{
                Name = $entry.Name; State = if ($found.Count -gt 0) { 'PRESENT' } else { 'ABSENT' }
                Released = ConvertTo-AdoLiveVersion (Get-AdoLivePropertyValue $first 'releasedVersion')
                Maximum = ConvertTo-AdoLiveVersion (Get-AdoLivePropertyValue $first 'maxVersion')
            }
        })
}

# A version as major.minor digits, or UNKNOWN: server text is never printed as it came.
function ConvertTo-AdoLiveVersion {
    param([AllowNull()][object] $Value)
    $match = [regex]::Match([string] $Value, '^([0-9]{1,3})\.([0-9]{1,3})(\.[0-9]{1,5})*$')
    if ($match.Success) { return $match.Groups[1].Value + '.' + $match.Groups[2].Value }
    return 'UNKNOWN'
}

# V-34: whether a result from a listing with details holds what the result read alone holds: the
# fields the toolkit reads from that read, as many sub-results and iterations, and a message and a
# trace of the same length. Names, counts and lengths are compared; no value is returned.
function Get-AdoLiveListedDetailAgreement {
    param([Parameter(Mandatory = $true)][object] $Listed, [Parameter(Mandatory = $true)][object] $Detail)
    $read = @('id', 'outcome', 'automatedTestName', 'automatedTestStorage', 'testCaseTitle', 'resultGroupType', 'startedDate', 'completedDate',
        'durationInMs', 'errorMessage', 'stackTrace', 'computerName', 'failureType', 'resolutionState', 'comment', 'priority', 'owner', 'runBy',
        'testRun', 'failingSince', 'testCase', 'associatedBugs', 'customFields', 'subResults', 'iterationDetails')
    $count = { param($Item, $Name) $value = Get-AdoLivePropertyValue $Item $Name; if ($null -eq $value) { 0 } else { @($value).Count } }
    $length = { param($Item, $Name) ([string] (Get-AdoLivePropertyValue $Item $Name)).Length }
    $missing = @($read | Where-Object { $null -ne $Detail.PSObject.Properties[$_] -and $null -eq $Listed.PSObject.Properties[$_] })
    return [pscustomobject]@{
        Fields = $missing.Count -eq 0
        SubResults = (& $count $Listed 'subResults') -eq (& $count $Detail 'subResults')
        Iterations = (& $count $Listed 'iterationDetails') -eq (& $count $Detail 'iterationDetails')
        Text = (& $length $Listed 'errorMessage') -eq (& $length $Detail 'errorMessage') -and (& $length $Listed 'stackTrace') -eq (& $length $Detail 'stackTrace')
    }
}

# V-35: the decoded length of a body as it came over the wire, by its content encoding. Only gzip
# and deflate are decoded; anything else counts as it came.
function Get-AdoLiveDecodedLength {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]] $Bytes, [AllowNull()][string] $ContentEncoding)
    $encoding = ([string] $ContentEncoding).Trim().ToLowerInvariant()
    if ($encoding -notin @('gzip', 'deflate')) { return [long] $Bytes.Length }
    $source = [System.IO.MemoryStream]::new($Bytes)
    $stream = if ($encoding -eq 'gzip') { [System.IO.Compression.GZipStream]::new($source, [System.IO.Compression.CompressionMode]::Decompress) }
    else { [System.IO.Compression.ZLibStream]::new($source, [System.IO.Compression.CompressionMode]::Decompress) }
    try {
        $buffer = [byte[]]::new(81920)
        [long] $total = 0
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) { $total += $read }
        return $total
    }
    finally { $stream.Dispose(); $source.Dispose() }
}

# V-35: COMPRESSED or UNCOMPRESSED, and the bytes sent as a whole percentage of the decoded bytes.
function Get-AdoLiveCompressionEvidence {
    param([AllowNull()][string] $ContentEncoding, [long] $WireBytes, [long] $DecodedBytes)
    $compressed = ([string] $ContentEncoding).Trim().ToLowerInvariant() -in @('gzip', 'deflate')
    return [pscustomobject]@{
        State = if ($compressed) { 'COMPRESSED' } else { 'UNCOMPRESSED' }
        Percent = if ($DecodedBytes -gt 0) { [int] [Math]::Round(100.0 * $WireBytes / $DecodedBytes) } else { 0 }
    }
}

# V-36: whether a result's own attachment list also holds the attachments of its sub-results.
# OVERLAP when it holds at least one of them; Complete when it holds them all.
function Get-AdoLiveAttachmentOverlap {
    param([AllowEmptyCollection()][int[]] $Result = @(), [AllowEmptyCollection()][int[]] $SubResult = @())
    $own = @($SubResult | Sort-Object -Unique)
    $shared = @($own | Where-Object { $Result -contains $_ })
    return [pscustomobject]@{
        State = if ($shared.Count -gt 0) { 'OVERLAP' } else { 'DISJOINT' }; Complete = $own.Count -gt 0 -and $shared.Count -eq $own.Count
        Counts = [ordered]@{ RESULT = @($Result | Sort-Object -Unique).Count; SUB_RESULTS = $own.Count; SHARED = $shared.Count }
    }
}

# V-37: the shape of an identity field value, by name only. IDENTITY_OBJECT names somebody through
# displayName, uniqueName or id; STRING is a non-empty string, the DOMAIN\user shape; EMPTY is JSON
# null or a blank string, which the toolkit reads as nobody assigned; OTHER is a shape the toolkit
# cannot name, including an object carrying none of the three. The value itself never leaves here.
function Get-AdoLiveIdentityShape {
    param([AllowNull()][object] $Value)
    if ($null -eq $Value) { return 'EMPTY' }
    if ($Value -is [string]) { if ([string]::IsNullOrWhiteSpace($Value)) { return 'EMPTY' } else { return 'STRING' } }
    if ($Value -is [psobject] -and -not ($Value -is [array]) -and -not ($Value -is [valuetype])) {
        foreach ($name in @('displayName', 'uniqueName', 'id')) {
            if (-not [string]::IsNullOrWhiteSpace([string] (Get-AdoLivePropertyValue $Value $name))) { return 'IDENTITY_OBJECT' }
        }
    }
    return 'OTHER'
}

# V-37: what a projected work item batch carried for System.CreatedDate and System.AssignedTo.
# CountedId are the bugs the module itself reported, the only ones its own counts can be compared
# with; AssignedId, when given, is a bug the developer knows is assigned, which is what tells an
# omitted field from nobody assigned. Only counts and shape names are returned.
function Get-AdoLiveBugFieldEvidence {
    param(
        [AllowEmptyCollection()][object[]] $Item = @(),
        [AllowEmptyCollection()][int[]] $CountedId = @(),
        [int] $AssignedId = 0
    )
    $returned = 0
    $created = 0
    $assigned = 0
    $unknown = 0
    $shapes = [System.Collections.Generic.List[string]]::new()
    $assignedReturned = $false
    $assignedPresent = $false
    foreach ($entry in $Item) {
        $fields = Get-AdoLivePropertyValue $entry 'fields'
        $id = 0
        if (-not [int]::TryParse([string] (Get-AdoLivePropertyValue $entry 'id'), [ref] $id)) { $id = 0 }
        $hasCreated = $null -ne $fields -and $null -ne $fields.PSObject.Properties['System.CreatedDate'] -and
            -not [string]::IsNullOrWhiteSpace([string] (Get-AdoLivePropertyValue $fields 'System.CreatedDate'))
        $hasAssigned = $null -ne $fields -and $null -ne $fields.PSObject.Properties['System.AssignedTo']
        $shape = if ($hasAssigned) { Get-AdoLiveIdentityShape (Get-AdoLivePropertyValue $fields 'System.AssignedTo') } else { 'ABSENT' }
        if ($hasAssigned) {
            $shapes.Add($shape)
            if ($shape -eq 'OTHER') { $unknown++ }
        }
        if ($AssignedId -gt 0 -and $id -eq $AssignedId) {
            $assignedReturned = $true
            $assignedPresent = $hasAssigned -and $shape -in @('IDENTITY_OBJECT', 'STRING')
        }
        if ($CountedId -notcontains $id) { continue }
        $returned++
        if ($hasCreated) { $created++ }
        if ($shape -in @('IDENTITY_OBJECT', 'STRING')) { $assigned++ }
    }
    return [pscustomobject]@{
        Returned = $returned; CreatedDate = $created; AssignedTo = $assigned; Unknown = $unknown
        Shape = @($shapes | Sort-Object -Unique)
        AssignedReturned = $assignedReturned; AssignedPresent = $assignedPresent
    }
}

# V-38: whether a check can run on the newest installed version. Update-AdoToolkit came with 0.11.0.
function Get-AdoLiveUpdateGate {
    param([AllowNull()][version] $Installed)
    if ($null -eq $Installed) { return 'INCONCLUSIVE V-38 INSTALLED_MODULE_REQUIRED' }
    if ($Installed -lt [version] '0.11.0') { return 'INCONCLUSIVE V-38 UPDATE_CMDLET_REQUIRED' }
    return $null
}

# V-38: a manifest with another ModuleVersion, so that a copy of the install runs as an older version.
function ConvertTo-AdoLiveRelabeledManifest {
    param([Parameter(Mandatory = $true)][string] $Text, [Parameter(Mandatory = $true)][string] $Version)
    $relabeled = [regex]::Replace($Text, "(?m)^(\s*ModuleVersion\s*=\s*)'[^']*'", "`${1}'$Version'")
    if ($relabeled -ceq $Text) { throw 'MANIFEST_VERSION_REQUIRED' }
    return $relabeled
}

# V-38: what the child process printed: the status and file count after Update-AdoToolkit, or its
# error ID, and for each host whether the system proxy carries it. Any other line is ignored.
function Read-AdoLiveUpdateOutput {
    param([AllowEmptyCollection()][AllowNull()][string[]] $Line = @())
    $status = $null
    $files = -1
    $errorId = $null
    $proxy = [ordered]@{}
    foreach ($text in @($Line)) {
        if ($text -cmatch '^STATUS ([A-Za-z]+)$') { $status = $Matches[1] }
        elseif ($text -cmatch '^FILES ([0-9]{1,6})$') { $files = [int] $Matches[1] }
        elseif ($text -cmatch '^ERROR (.*)$') { $errorId = $Matches[1] }
        elseif ($text -cmatch '^PROXY (API|SITE|DOWNLOAD) (DIRECT|PROXIED)$') { $proxy[$Matches[1]] = $Matches[2] }
    }
    return [pscustomobject]@{ Status = $status; Files = $files; ErrorId = $errorId; Proxy = $proxy }
}

# V-38: the seven module files installed through the proxy settle that the three GitHub hosts are
# reachable. A rate limit, a missing release, nothing to install, a configuration refusal (such as a
# release that needs a newer PowerShell, refused after its download) or a local file error cannot
# settle it; any other toolkit error, such as a proxy that refuses a host or a redirect to another
# host, contradicts it.
function Get-AdoLiveUpdateVerdict {
    param([AllowNull()][string] $Status, [int] $Files = -1, [AllowNull()][string] $ErrorId)
    if ($ErrorId) {
        if ($ErrorId -cnotmatch '^Ado[A-Za-z]+$') { return 'FAIL V-38 CHECK_FAILED' }
        if ($ErrorId -cin @('AdoThrottled', 'AdoNotFound', 'AdoConfiguration', 'AdoFileOutput')) { return "INCONCLUSIVE V-38 $ErrorId" }
        return "FAIL V-38 $ErrorId"
    }
    if ($Status -ceq 'Installed') {
        if ($Files -eq 7) { return 'PASS V-38 RELEASE_INSTALLED' }
        return 'FAIL V-38 INSTALL_INCOMPLETE'
    }
    if ($Status -cin @('UpToDate', 'AlreadyInstalled')) { return "INCONCLUSIVE V-38 $Status" }
    return 'FAIL V-38 CHECK_FAILED'
}

# V-38: DIRECT or PROXIED, from what the system proxy answers for a destination; never its address.
# Update.Live.ps1 runs it in the child process, the one whose client makes the requests.
function Get-AdoLiveProxyNote {
    param([AllowNull()][uri] $Proxy, [Parameter(Mandatory = $true)][uri] $Destination)
    if ($null -eq $Proxy -or $Proxy -eq $Destination) { return 'DIRECT' }
    return 'PROXIED'
}
