BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $liveRoot = Join-Path $PSScriptRoot '../../tests/Live'

    # Extract only pure declarations or one validation block. Never execute a live script,
    # load an installed module, read a profile, or contact a service in these tests.
    function Read-LiveAst {
        param([string] $Name)
        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $liveRoot "$Name.Live.ps1"), [ref] $tokens, [ref] $errors)
        if ($errors.Count) { throw 'Live script does not parse.' }
        return $ast
    }
    function Get-LiveDefinitions {
        param([string] $Name)
        $ast = Read-LiveAst $Name
        $definitions = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) |
            ForEach-Object { $_.Extent.Text })
        $opaque = $ast.FindAll({ param($node)
                $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$opaqueObjects'
            }, $true)
        $definitions += @($opaque | ForEach-Object { $_.Extent.Text })
        return [scriptblock]::Create($definitions -join "`n")
    }
    function Get-LiveSection {
        param([string] $Name, [string] $Marker)
        $candidates = (Read-LiveAst $Name).FindAll({ param($node)
                $node -is [System.Management.Automation.Language.TryStatementAst] -and $node.Extent.Text.Contains($Marker)
            }, $true)
        $section = $candidates | Sort-Object { $_.Extent.Text.Length } | Select-Object -First 1
        if ($null -eq $section) { throw 'Validation block not found.' }
        return [scriptblock]::Create($section.Extent.Text)
    }
    . (Get-LiveDefinitions 'TestFailures')
    . (Get-LiveDefinitions 'Bulk')
    . (Get-LiveDefinitions 'Shape')
    . (Get-LiveDefinitions 'Triage')
    $common = Join-Path $liveRoot 'Live.Common.ps1'
    if (Test-Path -LiteralPath $common) { . $common }
}

Describe 'Approved live-check audit regressions with synthetic data only' {
    BeforeEach {
        $checkStates = [System.Collections.Generic.List[string]]::new()
        $projectBase = 'https://ado.example.test/Collection/Project'
        $buildId = 401
        $rerunId = 402
        $reattemptId = 403
        $runs = @([pscustomobject]@{ id = 1 })
        $hasAttempt = $false
        Mock Invoke-WebRequest { throw 'Network forbidden in offline tests.' }
        Mock Invoke-RestMethod { throw 'Network forbidden in offline tests.' }
    }

    It 'F11 accepts aggregate-only runs with optional fields absent' {
        Mock Get-AdoLiveBuildRunList { [pscustomobject]@{
                Items = @([pscustomobject]@{ id = 1; name = 'synthetic'; totalTests = 1; passedTests = 1 }); Complete = $true } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'RUN_FIELDS_MISSING'))
        $lines -join "`n" | Should -Not -Match '^FAIL'
    }

    It 'F11 treats absent or null Test Case references as optional' -TestCases @(
        @{ Reference = '' }, @{ Reference = ',"testCase":{"id":null}' }
    ) {
        param($Reference)
        $results = @([pscustomobject]@{ id = 2; outcome = 'Failed' })
        $payload = '{"id":2,"errorMessage":"synthetic"' + $Reference + '}'
        Mock Invoke-AdoTestRequest { [pscustomobject]@{ Content = $payload } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'NO_DETAIL_FIELDS_PRESENT'))
        $lines -join "`n" | Should -Not -Match '^FAIL'
    }

    It 'F11 permits attachment lists without optional size and metadata' {
        $detail = [pscustomobject]@{ id = 2 }
        Mock Invoke-AdoTestRequest { [pscustomobject]@{
                Content = '{"value":[{"id":1,"fileName":"sample.txt"}]}'
                RawContentStream = [IO.MemoryStream]::new([byte[]]@(1, 2)) } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'ATTACHMENT_FIELDS_MISSING'))
        $lines -join "`n" | Should -Match 'PASS V-23'
    }

    It 'F11 treats manual results as insufficient evidence for automated history identity' {
        $build = [pscustomobject]@{ definition = [pscustomobject]@{ id = 42 }; queueTime = '2026-01-01T00:00:00Z' }
        $results = @([pscustomobject]@{ id = 2; outcome = 'Failed' })
        Mock Invoke-AdoTestRequest { [pscustomobject]@{ Content = '{"value":[{"id":400}]}' } }
        Mock Get-AdoLiveBuildRunList { [pscustomobject]@{ Items = @([pscustomobject]@{ id = 1 }); Complete = $true } }
        Mock Get-AdoLiveTopSkip { [pscustomobject]@{ Items = @([pscustomobject]@{ id = 2; outcome = 'Failed' }); Complete = $true } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'NO_IDENTITY_MATCHED_ACROSS_BUILDS'))
        $lines -join "`n" | Should -Match '^INCONCLUSIVE V-24'
    }

    It 'F11 allows an unfinished build without optional result or finish fields' {
        $buildText = '401'
        Mock Invoke-AdoTestRequest {
            param($Uri)
            $body = if ($Uri -match '/builds/401\?') {
                '{"id":401,"buildNumber":"synthetic","definition":{"id":42,"name":"synthetic"},"status":"inProgress","queueTime":"2026-01-01T00:00:00Z"}'
            }
            else { '{"value":[]}' }
            [pscustomobject]@{ Content = $body }
        }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BUILD_FIELDS_MISSING'))
        $lines -join "`n" | Should -Match '^INCONCLUSIVE V-25'
    }

    It 'F12 never confirms retries just because two build IDs were supplied' {
        $hasRerun = $true
        $hasReattempt = $true
        $detail = $null
        Mock Get-AdoLiveBuildRunList { [pscustomobject]@{ Items = @(); Complete = $true } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BOTH_RETRY_KINDS_OBSERVED'))
        $lines -join "`n" | Should -Match 'INCONCLUSIVE V-22'
    }

    It 'F12 requires complete observations and recognizes lowercase reruns and nested retry references' -TestCases @(
        @{ ListingComplete = $true; Verdict = 'PASS' }, @{ ListingComplete = $false; Verdict = 'INCONCLUSIVE' }
    ) {
        param($ListingComplete, $Verdict)
        $hasRerun = $true
        $hasReattempt = $true
        $detail = $null
        Mock Get-AdoLiveBuildRunList {
            param($BuildId)
            $items = if ($BuildId -eq 402) { @([pscustomobject]@{ id = 1 }) }
            else { @('{"id":3,"pipelineReference":{"jobReference":{"jobName":"synthetic","attempt":"2"}}}',
                    '{"id":2,"pipelineReference":{"jobReference":{"jobName":"synthetic","attempt":1}}}' | ConvertFrom-Json) }
            [pscustomobject]@{ Items = $items; Complete = $ListingComplete }
        }
        Mock Get-AdoLiveTopSkip { [pscustomobject]@{ Items = @([pscustomobject]@{ id = 2; resultGroupType = 'rerun' }); Complete = $ListingComplete } }
        Mock Invoke-AdoTestRequest { [pscustomobject]@{ Content = '{"id":2,"resultGroupType":"rerun","subResults":[{"id":1,"sequenceId":1,"outcome":"failed"},{"id":2,"sequenceId":2,"outcome":"passed"}]}' } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BOTH_RETRY_KINDS_OBSERVED'))
        $lines -join "`n" | Should -Match "^$Verdict V-22"
    }

    It 'F13 marks capped continuation enumeration incomplete' {
        $script:pageNumber = 0
        Mock Invoke-AdoLiveRequest {
            $script:pageNumber++
            [pscustomobject]@{ StatusCode = 200; Content = '{"value":[]}'; Headers = @{ 'x-ms-continuationtoken' = "page$script:pageNumber" } }
        }
        $page = Get-AdoLivePageSummary -BaseUri $projectBase -ApiVersion '6.0-preview.1'
        $page.Complete | Should -BeFalse
        $page.Pages | Should -Be 50
    }

    It 'F13 stops a repeated token without claiming completeness' {
        Mock Invoke-AdoLiveRequest { [pscustomobject]@{ StatusCode = 200; Content = '{"value":[]}'; Headers = @{ 'x-ms-continuationtoken' = 'same' } } }
        $page = Get-AdoLivePageSummary -BaseUri $projectBase -ApiVersion '6.0-preview.1'
        $page.Complete | Should -BeFalse
        $page.Pages | Should -Be 2
    }

    It 'F02 triage uses 64-bit log line counts in its range probe' {
        $buildBase = $projectBase + '/_apis/build/builds/401'
        # Stub the product command as well: no installed module is loaded by these tests.
        function Get-AdoBuild { [pscustomobject]@{ Id = 401 } }
        Mock Get-AdoTriagePageSummary { [pscustomobject]@{ Complete = $true; Continued = $true } }
        Mock Invoke-AdoTriageRequest {
            param($Uri)
            $content = if ($Uri -match '/logs\?') { '{"value":[{"id":1,"lineCount":"2147483648"}]}' }
            elseif ($Uri -match 'startLine=2147483646&') { "first`nlast" }
            else { 'last' }
            [pscustomobject]@{ Content = $content; Headers = @{ 'Content-Type' = 'application/json' } }
        }
        $lines = @(. (Get-LiveSection 'Triage' 'NONEMPTY_LOG_WITH_LINECOUNT_REQUIRED'))
        $lines -join "`n" | Should -Match '^PASS V-14'
    }

    It 'F14 records accepted fields plus expand as contradicting V-10' {
        $request = @{ Uri = $projectBase; Method = 'Post' }
        $body = '{}'
        Mock Invoke-RestMethod { [pscustomobject]@{ value = @() } }
        $lines = @(. (Get-LiveSection 'TestCase' 'COMBINATION_ACCEPTED'))
        $lines -join "`n" | Should -Match 'FAIL V-10.*ACCEPTED'
    }

    It 'F14 confirms rejection only when both individual control requests succeed' {
        $request = @{ Uri = $projectBase; Method = 'Post' }
        $caseId = 42
        $body = '{"ids":[42],"fields":["System.Title"],"$expand":"relations"}'
        Mock Invoke-RestMethod {
            param($Body)
            if ($Body.Contains('fields') -and $Body.Contains('$expand')) {
                $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::BadRequest)
                throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('synthetic', $response)
            }
            [pscustomobject]@{ value = @() }
        }
        $lines = @(. (Get-LiveSection 'TestCase' 'COMBINATION_ACCEPTED'))
        $lines -join "`n" | Should -Match '^PASS V-10 COMBINATION_REJECTED_CONTROLS_ACCEPTED'
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
    }

    It 'F15 does not infer formatting semantics from the presence of angle brackets' {
        $sourceItems = @([pscustomobject]@{ Fields = @{ 'Microsoft.VSTS.TCM.Steps' = @'
<steps><step><parameterizedString isformatted="true">Click Save</parameterizedString><parameterizedString isformatted="false">List&lt;T&gt;</parameterizedString></step></steps>
'@ } })
        $source = (Read-LiveAst 'TestCase').Extent.Text
        $start = $source.IndexOf('$validReferences = $true', [StringComparison]::Ordinal)
        $end = $source.IndexOf('$parameterCases = @($case)', [StringComparison]::Ordinal)
        $lines = @(. ([scriptblock]::Create($source.Substring($start, $end - $start))))
        $lines -join "`n" | Should -Not -Match 'FAIL V-02'
    }

    It 'F16 never includes dynamic keys from objects or arrays in shape paths' {
        $document = [System.Text.Json.JsonDocument]::Parse(@'
{"id":1,"CustomerAlpha":{"id":2},"customFields":[{"fieldName":"synthetic","value":{"CustomerBeta":{"id":3}}}],"links":{"CustomerGamma":{"href":"https://ado.example.test"}},"workItemFields":[{"CustomerDelta":"synthetic"}],"pipelineReference":{"jobReference":{"attempt":"2"}}}
'@)
        try {
            $shapes = [System.Collections.Generic.SortedDictionary[string, System.Collections.Generic.SortedSet[string]]]::new([StringComparer]::Ordinal)
            Add-AdoShape -Element $document.RootElement -Path '$' -Shapes $shapes
            $shapes.Keys -join "`n" | Should -Not -Match 'Customer'
            $shapes.Keys | Should -Contain '$.pipelineReference.jobReference.attempt'
        }
        finally { $document.Dispose() }
    }

    It 'F16 reports unknown outcome presence without printing the value' {
        Mock Get-AdoLiveTopSkip { [pscustomobject]@{ Complete = $true; Items = @([pscustomobject]@{
                        id = 2; outcome = 'CustomerEpsilon'; automatedTestName = 'synthetic'; automatedTestStorage = 'synthetic'
                        testCaseTitle = 'synthetic'; startedDate = '2026-01-01T00:00:00Z' }) } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'UNKNOWN_OUTCOME'))
        $lines -join "`n" | Should -Not -Match 'CustomerEpsilon'
    }

    It 'F16 omits dynamic keys from smoke exception paths too' {
        Get-AdoSafeJsonPath '$.customFields[0].value.CustomerZeta' | Should -Be 'UNPRINTABLE'
        Get-AdoSafeJsonPath '$.fields.SystemTitle' | Should -Be 'UNPRINTABLE'
        Get-AdoSafeJsonPath '$.value[0].testRun.id' | Should -Be '$.value[0].testRun.id'
    }
}
