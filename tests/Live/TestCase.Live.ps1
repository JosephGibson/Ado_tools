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

# Opt-in, installed module only (signed or unsigned); no work data leaves this process.
# Never persist responses or print values, URLs, identities, or exception messages.
$caseId = 0
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE) -or
    -not [int]::TryParse($env:ADOTOOLKIT_LIVE_TESTCASE_ID, [ref] $caseId) -or
    $caseId -lt 1 -or $caseId -eq [int]::MaxValue) {
    foreach ($item in @('V-01', 'V-02', 'V-03', 'V-05', 'V-10', 'V-13')) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_AND_CASE_REQUIRED" }
    exit 2
}
try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    # The newest installed release; a signed release must verify throughout, an unsigned one is accepted.
    $null = Import-AdoInstalledModule
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    $uri = $connection.CollectionUri.AbsoluteUri.TrimEnd('/') + '/_apis/wit/workitemsbatch?api-version=6.0'
    $headers = @{ 'Accept-Language' = $PSUICulture }
    $request = @{
        Uri = $uri; Method = 'Post'; UseDefaultCredentials = $true
        ContentType = 'application/json'; Headers = $headers
        MaximumRedirection = 0; TimeoutSec = $connection.RequestTimeoutSeconds
    }
    $body = @{ ids = @($caseId, [int]::MaxValue); errorPolicy = 'omit'; fields = @('System.Title') } | ConvertTo-Json -Compress
    try {
        $response = Invoke-RestMethod @request -Body $body
        if ($null -eq $response -or $null -eq $response.PSObject.Properties['value']) {
            Write-AdoLiveResult 'FAIL V-05 INVALID_RESPONSE'
        }
        else {
            $entries = @($response.value)
            $returned = @($entries | Where-Object { $null -ne $_ })
            if ($returned.Count -ne 1 -or $returned[0].id -ne $caseId) { Write-AdoLiveResult 'INCONCLUSIVE V-05 CASE_OR_SENTINEL_UNEXPECTED' }
            elseif (@($entries | Where-Object { $null -eq $_ }).Count -gt 0) { Write-AdoLiveResult 'PASS V-05 NULL_ENTRY' }
            else { Write-AdoLiveResult 'PASS V-05 OMITTED' }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-05 ERROR' }

    $body = @{ ids = @($caseId); errorPolicy = 'omit'; fields = @('System.Title'); '$expand' = 'relations' } | ConvertTo-Json -Compress
    try {
        Invoke-RestMethod @request -Body $body | Out-Null
        Write-AdoLiveResult 'FAIL V-10 COMBINATION_ACCEPTED_ASSUMPTION_CONTRADICTED'
    }
    catch {
        $httpResponse = $_.Exception.PSObject.Properties['Response']
        if ($null -ne $httpResponse -and $null -ne $httpResponse.Value -and [int] $httpResponse.Value.StatusCode -eq 400) {
            # A generic 400 alone proves nothing. Require each parameter to work separately.
            try {
                foreach ($control in @(
                        @{ ids = @($caseId); errorPolicy = 'omit'; fields = @('System.Title') },
                        @{ ids = @($caseId); errorPolicy = 'omit'; '$expand' = 'relations' })) {
                    Invoke-RestMethod @request -Body ($control | ConvertTo-Json -Compress) | Out-Null
                }
                Write-AdoLiveResult 'PASS V-10 COMBINATION_REJECTED_CONTROLS_ACCEPTED'
            }
            catch { Write-AdoLiveResult 'INCONCLUSIVE V-10 CONTROL_REQUEST_FAILED' }
        }
        else { Write-AdoLiveResult 'INCONCLUSIVE V-10 COMBINATION_REQUEST_FAILED' }
    }
    $case = Get-AdoTestCase -Id $caseId
    $sourceIds = @($caseId) + @($case.SharedSteps | ForEach-Object Id)
    $sourceItems = @(Get-AdoWorkItem -Id ($sourceIds | Select-Object -Unique) -Field Microsoft.VSTS.TCM.Steps)
    $validReferences = $true
    $hasChildren = $false
    $referenceCount = 0
    $formattedCount = 0
    $formatAgrees = $true
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.IgnoreComments = $true
    $settings.IgnoreProcessingInstructions = $true
    $settings.ValidationType = [System.Xml.ValidationType]::None
    $settings.MaxCharactersInDocument = 10485760
    foreach ($sourceItem in $sourceItems) {
        $xml = [string] $sourceItem.Fields['Microsoft.VSTS.TCM.Steps']
        if ([string]::IsNullOrWhiteSpace($xml)) { continue }
        $inputText = [System.IO.StringReader]::new($xml)
        $reader = [System.Xml.XmlReader]::Create($inputText, $settings)
        try {
            $document = [System.Xml.XmlDocument]::new()
            $document.XmlResolver = $null
            $document.Load($reader)
            foreach ($reference in $document.SelectNodes("//*[local-name()='compref']")) {
                $referenceCount++
                $referenceId = 0
                if (-not [int]::TryParse($reference.GetAttribute('ref'), [ref] $referenceId) -or $referenceId -lt 1) { $validReferences = $false }
                if ($reference.SelectNodes('*').Count -gt 0) { $hasChildren = $true }
            }
            foreach ($value in $document.SelectNodes("//*[local-name()='parameterizedString']")) {
                if ([string]::IsNullOrWhiteSpace($value.InnerText)) { continue }
                if ($value.GetAttribute('isformatted') -eq 'true') {
                    $formattedCount++
                }
                elseif ($value.GetAttribute('isformatted') -notin @('false', '')) { $formatAgrees = $false }
            }
        }
        finally { $reader.Dispose(); $inputText.Dispose() }
    }
    if (-not $validReferences) { Write-AdoLiveResult 'FAIL V-01 INVALID_REF' }
    elseif ($referenceCount -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-01 NO_REFERENCES' }
    elseif ($hasChildren) { Write-AdoLiveResult 'PASS V-01 POSITIVE_REF_CHILDREN_PRESENT' }
    else { Write-AdoLiveResult 'PASS V-01 POSITIVE_REF_NO_CHILDREN' }
    if (-not $formatAgrees) { Write-AdoLiveResult 'INCONCLUSIVE V-02 UNKNOWN_FORMAT_FLAG' }
    elseif ($formattedCount -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-02 NO_FORMATTED_VALUES' }
    else { Write-AdoLiveResult 'INCONCLUSIVE V-02 FORMAT_FLAGS_OBSERVED_VISUAL_COMPARISON_REQUIRED' }

    $parameterCases = @($case)
    $sharedCaseId = 0
    $sharedSupplied = [int]::TryParse($env:ADOTOOLKIT_LIVE_SHARED_PARAM_CASE_ID, [ref] $sharedCaseId) -and $sharedCaseId -gt 0
    if ($sharedSupplied) { $parameterCases += Get-AdoTestCase -Id $sharedCaseId }
    $parameterCodes = @($parameterCases | ForEach-Object Diagnostics | ForEach-Object Code)
    if ($parameterCodes -contains 'MalformedParameterData' -or $parameterCodes -contains 'UnresolvedSharedParameter') {
        Write-AdoLiveResult 'FAIL V-03 PARAMETER_DIAGNOSTIC'
    }
    elseif ($case.Parameters.Source -ne 'Local' -or -not $sharedSupplied -or $parameterCases[1].Parameters.Source -ne 'Shared') {
        Write-AdoLiveResult 'INCONCLUSIVE V-03 LOCAL_AND_SHARED_CASES_REQUIRED'
    }
    else {
        $accentedNames = @($parameterCases | ForEach-Object { $_.Parameters.Names } | Where-Object { $_ -match '[^\u0000-\u007F]' })
        $decoded = @($parameterCases | ForEach-Object { $_.Parameters.Rows } | ForEach-Object { $_.Keys } | Where-Object { $_ -in $accentedNames })
        if ($accentedNames.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-03 NO_ACCENTED_NAMES' }
        elseif ($decoded.Count -eq 0) { Write-AdoLiveResult 'FAIL V-03 ACCENTED_NAMES_NOT_JOINED' }
        else { Write-AdoLiveResult 'PASS V-03 LOCAL_SHARED_AND_ACCENTED_NAMES' }
    }

    $categoryBase = $connection.CollectionUri.AbsoluteUri.TrimEnd('/') + '/' + [uri]::EscapeDataString($case.TeamProject) + '/_apis/wit/workitemtypecategories/'
    $categoriesAgree = $true
    foreach ($category in @('Microsoft.TestCaseCategory', 'Microsoft.SharedStepCategory')) {
        $categoryResult = Invoke-RestMethod -Uri ($categoryBase + $category + '?api-version=6.0') -UseDefaultCredentials -Headers $headers -MaximumRedirection 0 -TimeoutSec $connection.RequestTimeoutSeconds
        if ($categoryResult.referenceName -ne $category -or @($categoryResult.workItemTypes).Count -eq 0) { $categoriesAgree = $false }
    }
    if ($categoriesAgree) { Write-AdoLiveResult 'PASS V-13 CATEGORIES_HAVE_MEMBERS' }
    else { Write-AdoLiveResult 'FAIL V-13 CATEGORY_MEMBERSHIP_DIFFERS' }
    # S1-7 also needs the developer's visual comparison with the ADO web UI.
    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    foreach ($item in @('V-01', 'V-02', 'V-03', 'V-05', 'V-10', 'V-13')) { Write-AdoLiveResult "FAIL $item CHECK_FAILED" }
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
