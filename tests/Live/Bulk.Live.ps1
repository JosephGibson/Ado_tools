[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'

$checkStates = [System.Collections.Generic.List[string]]::new()
function Write-AdoLiveResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $checkStates.Add($Text.Split(' ')[0])
    Write-Output $Text
}

# Opt-in, signed installed module only; no work data leaves this process and nothing is written.
# Never persist responses or print values, names, URLs, identities, tokens, or exception messages.
$planId = 0
$suiteId = 0
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE) -or
    -not [int]::TryParse($env:ADOTOOLKIT_LIVE_PLAN_ID, [ref] $planId) -or $planId -lt 1 -or
    -not [int]::TryParse($env:ADOTOOLKIT_LIVE_SUITE_ID, [ref] $suiteId) -or $suiteId -lt 1) {
    foreach ($item in @('V-04', 'V-06')) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_PLAN_AND_SUITE_REQUIRED" }
    exit 2
}

function Invoke-AdoLiveRequest {
    param(
        [Parameter(Mandatory = $true)][string] $Uri,
        [Parameter(Mandatory = $true)][string] $Method,
        [string] $Body
    )
    $request = @{
        Uri = $Uri; Method = $Method; UseDefaultCredentials = $true; SkipHttpErrorCheck = $true
        Headers = @{ 'Accept-Language' = $PSUICulture; Accept = 'application/json' }
        MaximumRedirection = 0; TimeoutSec = $connection.RequestTimeoutSeconds
    }
    if ($PSBoundParameters.ContainsKey('Body')) { $request.Body = $Body; $request.ContentType = 'application/json' }
    Invoke-WebRequest @request
}

function Get-AdoLivePageSummary {
    param([Parameter(Mandatory = $true)][string] $BaseUri, [Parameter(Mandatory = $true)][string] $ApiVersion)
    $pages = 0
    $tokens = 0
    $items = [System.Collections.Generic.List[object]]::new()
    $token = $null
    while ($pages -lt 50) {
        $query = 'api-version=' + [uri]::EscapeDataString($ApiVersion)
        if ($null -ne $token) { $query = 'continuationToken=' + [uri]::EscapeDataString($token) + '&' + $query }
        $response = Invoke-AdoLiveRequest -Uri ($BaseUri + '?' + $query) -Method Get
        $pages++
        if ([int] $response.StatusCode -ne 200) {
            return [pscustomobject]@{ Status = [int] $response.StatusCode; Pages = $pages; Tokens = $tokens; Items = @() }
        }
        $data = $response.Content | ConvertFrom-Json
        if ($null -eq $data -or $null -eq $data.PSObject.Properties['value']) { throw 'INVALID_RESPONSE' }
        foreach ($entry in @($data.value)) { $items.Add($entry) }
        $values = @($response.Headers['x-ms-continuationtoken'])
        $token = if ($values.Count -gt 0 -and -not [string]::IsNullOrEmpty([string] $values[0])) { [string] $values[0] } else { $null }
        if ($null -eq $token) { break }
        $tokens++
    }
    [pscustomobject]@{ Status = 200; Pages = $pages; Tokens = $tokens; Items = $items.ToArray() }
}

try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    $installedRoot = Resolve-AdoPackagePath -Path (Get-AdoModuleRoot)
    $candidate = Get-Module -ListAvailable -Name AdoToolkit | Select-Object -First 1
    if ($null -eq $candidate) { throw 'INSTALLED_MODULE_REQUIRED' }
    $package = Resolve-AdoPackagePath -Path $candidate.ModuleBase -Root $installedRoot
    $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $package 'AdoToolkit.psd1')
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) { throw 'SIGNATURE_INVALID' }
    Assert-AdoPackageSignature -PackagePath $package -ExpectedThumbprint $signature.SignerCertificate.Thumbprint
    Import-Module -Name (Join-Path $package 'AdoToolkit.psd1') -Force
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    if ([string]::IsNullOrWhiteSpace($connection.DefaultProject)) {
        foreach ($item in @('V-04', 'V-06')) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_DEFAULT_PROJECT_REQUIRED" }
        exit 2
    }
    $projectBase = $connection.CollectionUri.AbsoluteUri.TrimEnd('/') + '/' + [uri]::EscapeDataString($connection.DefaultProject)
    $planText = $planId.ToString([cultureinfo]::InvariantCulture)
    $suiteText = $suiteId.ToString([cultureinfo]::InvariantCulture)

    # V-04: pinned testplan preview routes, continuation behavior, shapes, and the GA test-area alternative.
    try {
        $plans = Get-AdoLivePageSummary -BaseUri ($projectBase + '/_apis/testplan/plans') -ApiVersion '6.0-preview.1'
        $suites = Get-AdoLivePageSummary -BaseUri ($projectBase + '/_apis/testplan/Plans/' + $planText + '/suites') -ApiVersion '6.0-preview.1'
        $cases = Get-AdoLivePageSummary -BaseUri ($projectBase + '/_apis/testplan/Plans/' + $planText + '/Suites/' + $suiteText + '/TestCase') -ApiVersion '6.0-preview.2'
        $ga = Invoke-AdoLiveRequest -Uri ($projectBase + '/_apis/test/Plans/' + $planText + '/suites/' + $suiteText + '/testcases?api-version=6.0') -Method Get
        $gaNote = 'GA_TEST_ROUTE_HTTP_' + ([int] $ga.StatusCode).ToString([cultureinfo]::InvariantCulture)
        $statuses = @($plans.Status, $suites.Status, $cases.Status)
        if (@($statuses | Where-Object { $_ -ne 200 }).Count -gt 0) {
            Write-AdoLiveResult ('FAIL V-04 PREVIEW_ROUTE_HTTP_' + (($statuses | ForEach-Object { $_.ToString([cultureinfo]::InvariantCulture) }) -join '_') + '_' + $gaNote)
        }
        else {
            $shapeAgrees = $true
            foreach ($plan in $plans.Items) {
                if ($null -eq $plan.PSObject.Properties['rootSuite'] -or $null -eq $plan.rootSuite -or [int] $plan.rootSuite.id -lt 1) { $shapeAgrees = $false }
            }
            $suiteIds = @($suites.Items | ForEach-Object { [int] $_.id })
            if ($suiteIds -notcontains $suiteId) { $shapeAgrees = $false }
            foreach ($suite in $suites.Items) {
                $parent = $suite.PSObject.Properties['parentSuite']
                if ($null -ne $parent -and $null -ne $parent.Value -and $suiteIds -notcontains [int] $parent.Value.id) { $shapeAgrees = $false }
            }
            foreach ($case in $cases.Items) {
                if ($null -eq $case.PSObject.Properties['workItem'] -or $null -eq $case.PSObject.Properties['order'] -or [int] $case.workItem.id -lt 1) { $shapeAgrees = $false }
            }
            $toolkitPlans = @(Get-AdoTestPlan -Id $planId)
            $toolkitSuites = @(Get-AdoTestSuite -PlanId $planId -SuiteId $suiteId -Recurse)
            if ($toolkitPlans.Count -ne 1 -or $toolkitSuites.Count -lt 1 -or $toolkitSuites[0].Id -ne $suiteId) { $shapeAgrees = $false }
            $tokenCount = $plans.Tokens + $suites.Tokens + $cases.Tokens
            if (-not $shapeAgrees) { Write-AdoLiveResult ('FAIL V-04 SHAPE_DIFFERS_' + $gaNote) }
            elseif ($tokenCount -eq 0) { Write-AdoLiveResult ('INCONCLUSIVE V-04 PREVIEW_ROUTES_200_NO_CONTINUATION_OBSERVED_' + $gaNote) }
            else { Write-AdoLiveResult ('PASS V-04 PREVIEW_ROUTES_200_CONTINUATION_FOLLOWED_' + $gaNote) }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-04 CHECK_FAILED' }

    # V-06: a broad flat query without $top; the WIQL text is fixed and structural.
    try {
        $wiql = 'SELECT [System.Id] FROM WorkItems ORDER BY [System.Id]'
        $body = @{ query = $wiql } | ConvertTo-Json -Compress
        $response = Invoke-AdoLiveRequest -Uri ($projectBase + '/_apis/wit/wiql?api-version=6.0') -Method Post -Body $body
        $status = [int] $response.StatusCode
        $failures = @()
        $toolkitOutput = @(Invoke-AdoWiql -Query $wiql -ErrorAction SilentlyContinue -ErrorVariable failures)
        $toolkitRejected = $toolkitOutput.Count -eq 0 -and $failures.Count -eq 1 -and $failures[0].FullyQualifiedErrorId -like 'AdoRequest,*'
        if ($status -eq 200) {
            $data = $response.Content | ConvertFrom-Json
            $count = @($data.workItems).Count
            if ($count -lt 20000) { Write-AdoLiveResult 'INCONCLUSIVE V-06 FEWER_THAN_CAP' }
            elseif (-not $toolkitRejected) { Write-AdoLiveResult 'FAIL V-06 CAP_REACHED_TOOLKIT_DID_NOT_REJECT' }
            elseif ($count -eq 20000) { Write-AdoLiveResult 'PASS V-06 HTTP_200_EXACTLY_CAP_TOOLKIT_REJECTED' }
            else { Write-AdoLiveResult 'PASS V-06 HTTP_200_ABOVE_CAP_TOOLKIT_REJECTED' }
        }
        else {
            $typeKey = 'TYPEKEY_ABSENT'
            $codeNote = 'CODE_ABSENT'
            try {
                $errorBody = $response.Content | ConvertFrom-Json
                if ($null -ne $errorBody.PSObject.Properties['typeKey'] -and -not [string]::IsNullOrEmpty([string] $errorBody.typeKey)) { $typeKey = 'TYPEKEY_PRESENT' }
                if ($null -ne $errorBody.PSObject.Properties['message'] -and [string] $errorBody.message -match 'VS402337') { $codeNote = 'CODE_VS402337' }
            }
            catch { $typeKey = 'BODY_NOT_JSON' }
            $note = 'HTTP_' + $status.ToString([cultureinfo]::InvariantCulture) + '_' + $typeKey + '_' + $codeNote
            if (-not $toolkitRejected) { Write-AdoLiveResult ('FAIL V-06 ' + $note + '_TOOLKIT_DID_NOT_REJECT') }
            elseif ($status -eq 400 -and $codeNote -eq 'CODE_VS402337') { Write-AdoLiveResult ('PASS V-06 ' + $note) }
            else { Write-AdoLiveResult ('FAIL V-06 ' + $note + '_FIXTURE_ASSUMPTION_DIFFERS') }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-06 CHECK_FAILED' }

    # S3-6 also needs the manual acceptance run in the Slice 3 plan, section 8; its outputs stay at work.
    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    foreach ($item in @('V-04', 'V-06')) { Write-AdoLiveResult "FAIL $item CHECK_FAILED" }
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
