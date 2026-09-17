[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Live.Common.ps1')

# Opt-in, installed module only (signed or unsigned). Records the wire shape of the responses the
# toolkit parses so synthetic fixtures can match the server. Holds responses in memory, writes
# nothing, and prints only:
#   SHAPE <operation> <JSON path> <JSON value kinds>
#   PASS|FAIL|INCONCLUSIVE <SHAPE-n> <structural note>
# Paths contain API-defined property names only: arrays collapse to [], and objects whose keys can
# be organization data (work item fields, links, properties) are reported without their keys.
# Values are never printed. Uses ADOTOOLKIT_LIVE_PROFILE (with a default project), and optionally
# ADOTOOLKIT_LIVE_TEST_BUILD_ID, ADOTOOLKIT_LIVE_PLAN_ID and ADOTOOLKIT_LIVE_SUITE_ID.
$checkStates = [System.Collections.Generic.List[string]]::new()
function Write-AdoLiveResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $checkStates.Add($Text.Split(' ')[0])
    Write-Output $Text
}

function Add-AdoShape {
    param(
        [Parameter(Mandatory = $true)][System.Text.Json.JsonElement] $Element,
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][System.Collections.Generic.SortedDictionary[string, System.Collections.Generic.SortedSet[string]]] $Shapes,
        [int] $Depth = 0,
        [string] $Schema = 'Entity'
    )
    if (-not $Shapes.ContainsKey($Path)) {
        $Shapes[$Path] = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
    }
    [void] $Shapes[$Path].Add($Element.ValueKind.ToString())
    if ($Depth -ge 12) { return }
    if ($Schema -in @('Opaque', 'Scalar')) {
        if ($Element.ValueKind -in @([System.Text.Json.JsonValueKind]::Object, [System.Text.Json.JsonValueKind]::Array)) {
            [void] $Shapes[$Path].Add('KeysOmitted')
        }
        return
    }
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $members = Get-AdoShapeMembers $Schema
        foreach ($property in $Element.EnumerateObject()) {
            $known = $members.ContainsKey($property.Name)
            $name = if ($known) { $property.Name } else { '<unknown>' }
            $child = if ($known) { $members[$property.Name] } else { 'Opaque' }
            Add-AdoShape -Element $property.Value -Path ($Path + '.' + $name) -Shapes $Shapes -Depth ($Depth + 1) -Schema $child
        }
    }
    elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        foreach ($item in $Element.EnumerateArray()) {
            Add-AdoShape -Element $item -Path ($Path + '[]') -Shapes $Shapes -Depth ($Depth + 1) -Schema $Schema
        }
    }
}

function Get-AdoLiveJson {
    param([Parameter(Mandatory = $true)][string] $Uri)
    $response = Invoke-WebRequest -Uri $Uri -Method Get -UseDefaultCredentials -SkipHttpErrorCheck -MaximumRedirection 0 `
        -Headers @{ Accept = 'application/json' } -TimeoutSec $connection.RequestTimeoutSeconds
    if ([int] $response.StatusCode -ne 200) { throw ('HTTP_' + [int] $response.StatusCode) }
    return [System.Text.Json.JsonDocument]::Parse(([string] $response.Content).TrimStart([char] 0xfeff))
}

# Prints the shape of one response; $script:lastDocument keeps it for follow-up requests.
function Write-AdoShape {
    param([Parameter(Mandatory = $true)][string] $Id, [Parameter(Mandatory = $true)][string] $Operation, [Parameter(Mandatory = $true)][string] $Uri)
    $script:lastDocument = $null
    try {
        $document = Get-AdoLiveJson -Uri $Uri
        $shapes = [System.Collections.Generic.SortedDictionary[string, System.Collections.Generic.SortedSet[string]]]::new([System.StringComparer]::Ordinal)
        Add-AdoShape -Element $document.RootElement -Path '$' -Shapes $shapes
        foreach ($entry in $shapes.GetEnumerator()) { Write-Output ('SHAPE {0} {1} {2}' -f $Operation, $entry.Key, ($entry.Value -join '|')) }
        Write-AdoLiveResult "PASS $Id $Operation"
        $script:lastDocument = $document
    }
    catch {
        $reason = if ($_.Exception.Message -match '^HTTP_[1-5][0-9]{2}$') { $_.Exception.Message } else { 'REQUEST_FAILED' }
        Write-AdoLiveResult "FAIL $Id $Operation $reason"
    }
}

function Get-AdoProperty {
    param([System.Text.Json.JsonElement] $Element, [string] $Name)
    if ($Element.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { return $null }
    foreach ($property in $Element.EnumerateObject()) { if ($property.Name -ceq $Name) { return $property.Value } }
    return $null
}

function Get-AdoFirstValue {
    param([System.Text.Json.JsonDocument] $Document, [scriptblock] $Where = { $true })
    if ($null -eq $Document) { return $null }
    $value = Get-AdoProperty -Element $Document.RootElement -Name 'value'
    if ($null -eq $value -or $value.ValueKind -ne [System.Text.Json.JsonValueKind]::Array) { return $null }
    foreach ($item in $value.EnumerateArray()) { if (& $Where $item) { return $item } }
    return $null
}

function Get-AdoId {
    param([System.Text.Json.JsonElement] $Element)
    $id = Get-AdoProperty -Element $Element -Name 'id'
    if ($null -eq $id) { return $null }
    $text = if ($id.ValueKind -eq [System.Text.Json.JsonValueKind]::Number) { $id.GetRawText() } else { $id.ToString() }
    $number = 0
    if ([int]::TryParse($text, [ref] $number) -and $number -gt 0) { return $number }
    return $null
}

if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE)) {
    Write-AdoLiveResult 'INCONCLUSIVE SHAPE-1 PROFILE_REQUIRED'
    exit 2
}
try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    $null = Import-AdoInstalledModule
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    if ([string]::IsNullOrWhiteSpace($connection.DefaultProject)) {
        Write-AdoLiveResult 'INCONCLUSIVE SHAPE-1 PROFILE_DEFAULT_PROJECT_REQUIRED'
        exit 2
    }
    $collection = $connection.CollectionUri.AbsoluteUri.TrimEnd('/')
    $project = $collection + '/' + [uri]::EscapeDataString($connection.DefaultProject)
    Write-AdoShape 'SHAPE-1' 'ProjectsList' ($collection + '/_apis/projects?api-version=6.0&%24top=5')

    $buildId = 0
    if (-not ([int]::TryParse($env:ADOTOOLKIT_LIVE_TEST_BUILD_ID, [ref] $buildId) -and $buildId -gt 0)) {
        foreach ($item in @('SHAPE-2', 'SHAPE-3', 'SHAPE-4', 'SHAPE-5', 'SHAPE-6', 'SHAPE-10', 'SHAPE-11', 'SHAPE-12')) { Write-AdoLiveResult "INCONCLUSIVE $item TEST_BUILD_REQUIRED" }
    }
    else {
        $buildText = $buildId.ToString([cultureinfo]::InvariantCulture)
        Write-AdoShape 'SHAPE-2' 'BuildGet' ($project + '/_apis/build/builds/' + $buildText + '?api-version=6.0')
        Write-AdoShape 'SHAPE-10' 'BuildLogsList' ($project + '/_apis/build/builds/' + $buildText + '/logs?api-version=6.0')
        Write-AdoShape 'SHAPE-11' 'BuildTimeline' ($project + '/_apis/build/builds/' + $buildText + '/timeline?api-version=6.0')
        $buildUri = [uri]::EscapeDataString('vstfs:///Build/Build/' + $buildText)
        Write-AdoShape 'SHAPE-3' 'TestRunsList' ($project + '/_apis/test/runs?api-version=6.0&buildUri=' + $buildUri + '&includeRunDetails=true&%24top=100&%24skip=0')
        $run = Get-AdoFirstValue -Document $script:lastDocument
        $runId = if ($null -ne $run) { Get-AdoId -Element $run } else { $null }
        if ($null -eq $runId) {
            foreach ($item in @('SHAPE-4', 'SHAPE-5', 'SHAPE-6', 'SHAPE-12')) { Write-AdoLiveResult "INCONCLUSIVE $item NO_TEST_RUN" }
        }
        else {
            $runText = $runId.ToString([cultureinfo]::InvariantCulture)
            Write-AdoShape 'SHAPE-4' 'TestResultsList' ($project + '/_apis/test/Runs/' + $runText + '/results?api-version=6.0&detailsToInclude=None&%24top=200&%24skip=0')
            $failed = Get-AdoFirstValue -Document $script:lastDocument -Where {
                param($item)
                $outcome = Get-AdoProperty -Element $item -Name 'outcome'
                $null -ne $outcome -and $outcome.ValueKind -eq [System.Text.Json.JsonValueKind]::String -and $outcome.GetString() -ne 'Passed'
            }
            $resultId = if ($null -ne $failed) { Get-AdoId -Element $failed } else { $null }
            if ($null -eq $resultId) {
                foreach ($item in @('SHAPE-5', 'SHAPE-6', 'SHAPE-12')) { Write-AdoLiveResult "INCONCLUSIVE $item NO_UNSUCCESSFUL_RESULT_IN_FIRST_RUN" }
            }
            else {
                $resultBase = $project + '/_apis/test/Runs/' + $runText + '/results/' + $resultId.ToString([cultureinfo]::InvariantCulture)
                Write-AdoShape 'SHAPE-5' 'TestResultGet' ($resultBase + '?api-version=6.0&detailsToInclude=Iterations%2CWorkItems%2CSubResults')
                $subId = $null
                if ($null -ne $script:lastDocument) {
                    $subResults = Get-AdoProperty -Element $script:lastDocument.RootElement -Name 'subResults'
                    if ($null -ne $subResults -and $subResults.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
                        foreach ($subResult in $subResults.EnumerateArray()) {
                            $subId = Get-AdoId -Element $subResult
                            if ($null -ne $subId) { break }
                        }
                    }
                }
                Write-AdoShape 'SHAPE-6' 'TestResultAttachmentsList' ($project + '/_apis/test/Runs/' + $runText + '/Results/' +
                        $resultId.ToString([cultureinfo]::InvariantCulture) + '/attachments?api-version=6.0-preview.1')
                if ($null -ne $subId) {
                    Write-AdoShape 'SHAPE-12' 'TestSubResultAttachmentsList' ($resultBase + '/attachments?api-version=6.0-preview.1&testSubResultId=' +
                            $subId.ToString([cultureinfo]::InvariantCulture))
                }
                else { Write-AdoLiveResult 'INCONCLUSIVE SHAPE-12 NO_SUBRESULT_IN_SAMPLED_DETAIL' }
            }
        }
    }

    $planId = 0
    $suiteId = 0
    if (-not ([int]::TryParse($env:ADOTOOLKIT_LIVE_PLAN_ID, [ref] $planId) -and $planId -gt 0)) {
        foreach ($item in @('SHAPE-7', 'SHAPE-8', 'SHAPE-9')) { Write-AdoLiveResult "INCONCLUSIVE $item PLAN_REQUIRED" }
    }
    else {
        $planText = $planId.ToString([cultureinfo]::InvariantCulture)
        Write-AdoShape 'SHAPE-7' 'TestPlansList' ($project + '/_apis/testplan/plans?api-version=6.0-preview.1')
        Write-AdoShape 'SHAPE-8' 'TestSuitesForPlan' ($project + '/_apis/testplan/Plans/' + $planText + '/suites?api-version=6.0-preview.1')
        if (-not ([int]::TryParse($env:ADOTOOLKIT_LIVE_SUITE_ID, [ref] $suiteId) -and $suiteId -gt 0)) {
            Write-AdoLiveResult 'INCONCLUSIVE SHAPE-9 SUITE_REQUIRED'
        }
        else {
            Write-AdoShape 'SHAPE-9' 'SuiteTestCaseList' ($project + '/_apis/testplan/Plans/' + $planText + '/Suites/' +
                    $suiteId.ToString([cultureinfo]::InvariantCulture) + '/TestCase?api-version=6.0-preview.2')
        }
    }

    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    Write-AdoLiveResult 'FAIL SHAPE-1 CHECK_FAILED'
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
