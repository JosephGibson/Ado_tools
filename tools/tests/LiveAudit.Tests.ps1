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
    . (Get-LiveDefinitions 'TestCaseDetail')
    . (Get-LiveDefinitions 'Probes')
    . (Join-Path $liveRoot 'Live.Common.ps1')
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

    It 'names the field that an attachment lacks instead of failing on its absence' {
        $detail = [pscustomobject]@{ id = 2 }
        Mock Invoke-AdoTestRequest { [pscustomobject]@{ Content = '{"value":[{"fileName":"sample.txt"}]}' } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'ATTACHMENT_FIELDS_MISSING'))
        $lines | Should -Be @('FAIL V-23 ATTACHMENT_FIELDS_MISSING id')
        Should -Invoke Invoke-AdoTestRequest -Exactly -Times 1
    }

    It 'names the field that a build lacks instead of failing on its absence' {
        $buildText = '401'
        Mock Invoke-AdoTestRequest { [pscustomobject]@{ Content = '{"id":401,"buildNumber":"synthetic"}' } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BUILD_FIELDS_MISSING'))
        $lines | Should -Be @('FAIL V-25 BUILD_FIELDS_MISSING definition')
        Should -Invoke Invoke-AdoTestRequest -Exactly -Times 1
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
        $lines | Should -Contain 'INCONCLUSIVE V-02 FORMAT_FLAGS_OBSERVED_VISUAL_COMPARISON_REQUIRED'
    }

    # V-01: the toolkit skips the children of a shared-step reference. That is right only when
    # they repeat the Shared Steps work item's own steps; other nested steps would be lost.
    It 'V-01 classifies the children of shared-step references as <Expected>' -TestCases @(
        @{ Expected = 'NoChildren'; Case = '<steps><step><parameterizedString>CustomerAlpha</parameterizedString></step><compref id="3" ref="20" /></steps>' }
        @{ Expected = 'RepeatsSharedSteps'; Case = '<steps><compref id="3" ref="20"><step><parameterizedString>CustomerShared</parameterizedString><parameterizedString>CustomerExpected</parameterizedString></step></compref></steps>' }
        @{ Expected = 'OtherSteps'; Case = '<steps><compref id="3" ref="20"><step><parameterizedString>CustomerFollowing</parameterizedString></step></compref></steps>' }
        @{ Expected = 'OtherSteps'; Case = '<steps><compref id="3" ref="20"><step><parameterizedString>CustomerShared</parameterizedString><parameterizedString>CustomerExpected</parameterizedString></step><compref id="5" ref="30" /></compref></steps>' }
        @{ Expected = 'Unknown'; Case = '<steps><compref id="3" ref="21"><step /></compref></steps>' }
        @{ Expected = 'None'; Case = '<steps><step /></steps>' }
    ) {
        param($Expected, $Case)
        $shared = [xml] '<steps><step><parameterizedString>CustomerShared</parameterizedString><parameterizedString>CustomerExpected</parameterizedString></step></steps>'
        @(Get-AdoLiveComprefEvidence -Document ([xml] $Case) -ReferencedSteps @{ '20' = $shared }) | Should -Be @($Expected)
    }

    It 'V-01 reports <Verdict> for the reference children of a test case' -TestCases @(
        @{ Children = '<step><parameterizedString>CustomerFollowing</parameterizedString></step>'; Verdict = 'FAIL V-01 REFERENCE_CHILDREN_ARE_NOT_THE_SHARED_STEPS' }
        @{ Children = '<step><parameterizedString>CustomerShared</parameterizedString></step>'; Verdict = 'PASS V-01 POSITIVE_REF_CHILDREN_REPEAT_SHARED_STEPS' }
        @{ Children = ''; Verdict = 'PASS V-01 POSITIVE_REF_NO_CHILDREN' }
    ) {
        param($Children, $Verdict)
        $sourceItems = @(
            [pscustomobject]@{ Id = 10; Fields = @{ 'Microsoft.VSTS.TCM.Steps' = '<steps><compref id="2" ref="20">' + $Children + '</compref></steps>' } }
            [pscustomobject]@{ Id = 20; Fields = @{ 'Microsoft.VSTS.TCM.Steps' = '<steps><step><parameterizedString>CustomerShared</parameterizedString></step></steps>' } }
        )
        $source = (Read-LiveAst 'TestCase').Extent.Text
        $start = $source.IndexOf('$validReferences = $true', [StringComparison]::Ordinal)
        $end = $source.IndexOf('$parameterCases = @($case)', [StringComparison]::Ordinal)
        $text = @(. ([scriptblock]::Create($source.Substring($start, $end - $start)))) -join "`n"
        $text | Should -Match ('(?m)^' + [regex]::Escape($Verdict) + '$')
        $text | Should -Not -Match 'Customer'
    }

    It 'holds a check pending until it has a verdict' {
        Get-AdoLivePendingCheck -Id @('V-01', 'V-02', 'V-03') -Line @('PASS V-02 SYNTHETIC', 'NOTE V-03 COUNTS=1') | Should -Be @('V-01', 'V-03')
        Get-AdoLivePendingCheck -Id @('V-01', 'V-02') -Line @() | Should -Be @('V-01', 'V-02')
        Get-AdoLivePendingCheck -Id @('V-01') -Line @('FAIL V-01 SYNTHETIC', 'INCONCLUSIVE V-02 SYNTHETIC') | Should -BeNullOrEmpty
    }

    # A check that printed its verdict before a later step threw must not be reported a second time.
    It 'reports CHECK_FAILED for a Test Case check only when it has no verdict yet' {
        . (Get-LiveDefinitions 'TestCase')
        $items = @('V-01', 'V-02', 'V-03', 'V-05', 'V-10', 'V-13')
        $checkLines = [System.Collections.Generic.List[string]]::new()
        $null = Write-AdoLiveResult 'PASS V-05 OMITTED'
        $null = Write-AdoLiveResult 'INCONCLUSIVE V-10 CONTROL_REQUEST_FAILED'
        $loop = (Read-LiveAst 'TestCase').FindAll({ param($node)
                $node -is [System.Management.Automation.Language.ForEachStatementAst] -and $node.Extent.Text.Contains('CHECK_FAILED')
            }, $true) | Select-Object -First 1
        $lines = @(. ([scriptblock]::Create($loop.Extent.Text)))
        $lines | Should -Be @('FAIL V-01 CHECK_FAILED', 'FAIL V-02 CHECK_FAILED', 'FAIL V-03 CHECK_FAILED', 'FAIL V-13 CHECK_FAILED')
    }

    It 'V-01 stays inconclusive when the referenced Shared Steps were not read' {
        $sourceItems = @([pscustomobject]@{ Id = 10; Fields = @{ 'Microsoft.VSTS.TCM.Steps' = '<steps><compref id="2" ref="20"><step /></compref></steps>' } })
        $source = (Read-LiveAst 'TestCase').Extent.Text
        $start = $source.IndexOf('$validReferences = $true', [StringComparison]::Ordinal)
        $end = $source.IndexOf('$parameterCases = @($case)', [StringComparison]::Ordinal)
        $text = @(. ([scriptblock]::Create($source.Substring($start, $end - $start)))) -join "`n"
        $text | Should -Match '(?m)^INCONCLUSIVE V-01 REFERENCE_CHILDREN_PRESENT_SHARED_STEPS_NOT_READ$'
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

    It 'V-30 reports the bug lookup as <Verdict> with counts only' -TestCases @(
        @{ Categories = @('InProgress', 'Completed'); Bugs = 2; Codes = @(); Verdict = 'PASS V-30 BUG_ROUTES_AND_LOOKUP_AGREE' },
        @{ Categories = @('InProgress', 'CustomerKappa'); Bugs = 2; Codes = @(); Verdict = 'FAIL V-30 STATE_CATEGORIES_DIFFER' },
        @{ Categories = @('Completed'); Bugs = 1; Codes = @('BugMetadataUnavailable'); Verdict = 'FAIL V-30 MODULE_LOOKUP_DEGRADED' },
        @{ Categories = @('Completed'); Bugs = 0; Codes = @(); Verdict = 'INCONCLUSIVE V-30 NO_BUGS_ON_REPORTED_TESTS' }
    ) {
        param($Categories, $Bugs, $Codes, $Verdict)
        $detail = [pscustomobject]@{ id = 2; testCase = [pscustomobject]@{ id = '1010' } }
        $connection = [pscustomobject]@{ CollectionUri = [uri] 'https://ado.example.test/Collection'; RequestTimeoutSeconds = 30 }
        Mock Invoke-AdoTestRequest {
            param($Uri)
            $content = if ($Uri -match 'workitemtypecategories') { '{"workItemTypes":[{"name":"CustomerTheta"}]}' }
            else { '{"value":[' + (@($Categories | ForEach-Object { '{"name":"CustomerIota","category":"' + $_ + '"}' }) -join ',') + ']}' }
            [pscustomobject]@{ Content = $content }
        }
        Mock Invoke-AdoTestBatch { [pscustomobject]@{ Content = '{"value":[{"id":1010,"relations":[' +
                '{"rel":"System.LinkTypes.Related","url":"https://ado.example.test/Collection/_apis/wit/workItems/3001"},' +
                '{"rel":"Hyperlink","url":"https://ado.example.test/CustomerLambda"}]}]}' } }
        # Stub the product command as well: no installed module is loaded by these tests.
        function Get-AdoBuildTestFailure {
            $list = @(for ($index = 0; $index -lt $Bugs; $index++) {
                    [pscustomobject]@{ Id = 3001 + $index; Title = 'CustomerMu'; State = 'CustomerNu'; IsOpen = $true; IsLinkedToTestCase = $true } })
            [pscustomobject]@{ Failures = @([pscustomobject]@{ Bugs = $list }); Diagnostics = @($Codes | ForEach-Object { [pscustomobject]@{ Code = $_ } }) }
        }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BUG_CATEGORY_EMPTY'))
        $text = $lines -join "`n"
        $text | Should -Match ('^' + [regex]::Escape($Verdict) + ' ')
        $text | Should -Match 'WORKITEM_LINKS=1$'
        $text | Should -Not -Match 'Customer'
        Should -Invoke Invoke-AdoTestRequest -Exactly -Times 2 -ParameterFilter { $Uri -match '6\.0-preview\.1$' -or $Uri -match 'Microsoft\.BugCategory' }
    }

    # V-37: the two bug fields of 0.9.15, read from the module's own projection.
    It 'V-37 names an identity shape without reading its value, for <Case>' -TestCases @(
        @{ Case = 'an identity object'; Value = [pscustomobject]@{ displayName = 'CustomerAlpha'; uniqueName = 'CustomerBeta'; id = 'CustomerGamma' }; Shape = 'IDENTITY_OBJECT' }
        @{ Case = 'an object named only by its unique name'; Value = [pscustomobject]@{ uniqueName = 'CustomerBeta' }; Shape = 'IDENTITY_OBJECT' }
        @{ Case = 'an object named only by its id'; Value = [pscustomobject]@{ id = 'CustomerGamma' }; Shape = 'IDENTITY_OBJECT' }
        @{ Case = 'an object naming nobody'; Value = [pscustomobject]@{ imageUrl = 'https://ado.example.test/avatar' }; Shape = 'OTHER' }
        @{ Case = 'a bare string'; Value = 'CustomerDelta'; Shape = 'STRING' }
        @{ Case = 'a blank string'; Value = '   '; Shape = 'EMPTY' }
        @{ Case = 'JSON null'; Value = $null; Shape = 'EMPTY' }
        @{ Case = 'a number'; Value = 42; Shape = 'OTHER' }
    ) {
        param($Case, $Value, $Shape)
        $result = Get-AdoLiveIdentityShape $Value
        $result | Should -Be $Shape
        $result | Should -Not -Match 'Customer'
    }

    It 'V-37 counts the two fields over the bugs the module read, and the named assigned bug apart' {
        $items = @(
            [pscustomobject]@{ id = 3001; fields = [pscustomobject]@{ 'System.CreatedDate' = '2026-09-15T22:05:00Z'; 'System.AssignedTo' = [pscustomobject]@{ displayName = 'CustomerAlpha' } } }
            [pscustomobject]@{ id = 3002; fields = [pscustomobject]@{ 'System.CreatedDate' = '2026-09-10T11:00:00Z' } }
            [pscustomobject]@{ id = 3003; fields = [pscustomobject]@{ 'System.CreatedDate' = '2026-09-11T11:00:00Z'; 'System.AssignedTo' = $null } }
            # Not among the module's bugs: its shape is still evidence, its counts are not.
            [pscustomobject]@{ id = 9001; fields = [pscustomobject]@{ 'System.CreatedDate' = '2026-09-12T11:00:00Z'; 'System.AssignedTo' = 'CustomerDelta' } }
        )
        $evidence = Get-AdoLiveBugFieldEvidence -Item $items -CountedId @(3001, 3002, 3003) -AssignedId 9001
        $evidence.Returned | Should -Be 3
        $evidence.CreatedDate | Should -Be 3
        $evidence.AssignedTo | Should -Be 1
        $evidence.Unknown | Should -Be 0
        $evidence.Shape | Should -Be @('EMPTY', 'IDENTITY_OBJECT', 'STRING')
        $evidence.AssignedReturned | Should -BeTrue
        $evidence.AssignedPresent | Should -BeTrue
        ($evidence | Out-String) | Should -Not -Match 'Customer'
    }

    It 'V-37 sees an absent field and an unnameable identity' {
        $items = @(
            [pscustomobject]@{ id = 3001; fields = [pscustomobject]@{ 'System.State' = 'CustomerEpsilon' } }
            [pscustomobject]@{ id = 3002; fields = [pscustomobject]@{ 'System.CreatedDate' = '2026-09-10T11:00:00Z'; 'System.AssignedTo' = [pscustomobject]@{ imageUrl = 'https://ado.example.test/a' } } }
        )
        $evidence = Get-AdoLiveBugFieldEvidence -Item $items -CountedId @(3001, 3002) -AssignedId 0
        $evidence.CreatedDate | Should -Be 1
        $evidence.Unknown | Should -Be 1
        $evidence.AssignedReturned | Should -BeFalse
        # A named bug that never came back cannot settle the assignee half.
        $absent = Get-AdoLiveBugFieldEvidence -Item $items -CountedId @(3001) -AssignedId 9001
        $absent.AssignedReturned | Should -BeFalse
        $absent.AssignedPresent | Should -BeFalse
    }

    It 'V-37 reports <Verdict> for the projected bug read, with counts and shapes only' -TestCases @(
        @{ Verdict = 'PASS V-37 BUG_FIELDS_PRESENT_AND_SHAPED'; Assigned = 'CustomerAlpha'; Created = '2026-09-15T22:05:00Z'; Resolved = $true; Named = 0; ModuleAssigned = $true }
        @{ Verdict = 'INCONCLUSIVE V-37 NO_ASSIGNED_BUG'; Assigned = $null; Created = '2026-09-15T22:05:00Z'; Resolved = $true; Named = 0; ModuleAssigned = $false }
        @{ Verdict = 'INCONCLUSIVE V-37 NO_BUGS_ON_REPORTED_TESTS'; Assigned = 'CustomerAlpha'; Created = '2026-09-15T22:05:00Z'; Resolved = $false; Named = 0; ModuleAssigned = $true }
        @{ Verdict = 'FAIL V-37 CREATED_DATE_ABSENT'; Assigned = 'CustomerAlpha'; Created = $null; Resolved = $true; Named = 0; ModuleAssigned = $true }
        @{ Verdict = 'FAIL V-37 ASSIGNED_TO_SHAPE_UNKNOWN=1'; Assigned = 'OBJECT_WITHOUT_NAME'; Created = '2026-09-15T22:05:00Z'; Resolved = $true; Named = 0; ModuleAssigned = $true }
        @{ Verdict = 'FAIL V-37 ASSIGNED_TO_OMITTED'; Assigned = 'CustomerAlpha'; Created = '2026-09-15T22:05:00Z'; Resolved = $true; Named = 3001; ModuleAssigned = $true }
        @{ Verdict = 'FAIL V-37 MODULE_COUNTS_DIFFER'; Assigned = 'CustomerAlpha'; Created = '2026-09-15T22:05:00Z'; Resolved = $true; Named = 0; ModuleAssigned = $false }
    ) {
        param($Verdict, $Assigned, $Created, $Resolved, $Named, $ModuleAssigned)
        $connection = [pscustomobject]@{ CollectionUri = [uri] 'https://ado.example.test/Collection'; RequestTimeoutSeconds = 30 }
        $assignedBugId = $Named
        $hasAssignedBug = $Named -gt 0
        $reportedBugs = @([pscustomobject]@{
                Id = 3001; IsResolved = $Resolved
                CreatedDate = if ($null -eq $Created) { $null } else { [datetimeoffset] '2026-09-15T22:05:00Z' }
                AssignedTo = if ($ModuleAssigned) { [pscustomobject]@{ DisplayName = 'CustomerAlpha' } } else { $null }
            })
        # The named assigned bug, when one is given, is the same ID and comes back without the field.
        $fields = @()
        if ($null -ne $Created) { $fields += '"System.CreatedDate":"' + $Created + '"' }
        if ($Named -eq 0 -and $null -ne $Assigned) {
            $fields += if ($Assigned -eq 'OBJECT_WITHOUT_NAME') { '"System.AssignedTo":{"imageUrl":"https://ado.example.test/a"}' }
            else { '"System.AssignedTo":{"displayName":"' + $Assigned + '"}' }
        }
        Mock Invoke-AdoTestBatch { [pscustomobject]@{ Content = '{"value":[{"id":3001,"fields":{' + ($fields -join ',') + '}}]}' } }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BUG_FIELDS_PRESENT_AND_SHAPED'))
        $text = $lines -join "`n"
        $text | Should -Match ([regex]::Escape($Verdict))
        $text | Should -Not -Match 'Customer'
        $text | Should -Not -Match '2026-09'
        # One verdict for the ID, after its note at most.
        @($lines | Where-Object { $_ -match '^(PASS|FAIL|INCONCLUSIVE) V-37 ' }).Count | Should -Be 1
    }

    # The module reads bugs in batches of 200 IDs; one larger request would be refused by the server
    # and read as a V-37 failure that says nothing about the two fields.
    It 'V-37 reads more than 200 bugs in batches of at most 200 IDs' {
        $connection = [pscustomobject]@{ CollectionUri = [uri] 'https://ado.example.test/Collection'; RequestTimeoutSeconds = 30 }
        $assignedBugId = 0
        $hasAssignedBug = $false
        $reportedBugs = @(foreach ($id in 3001..3201) {
                [pscustomobject]@{
                    Id = $id; IsResolved = $true; CreatedDate = [datetimeoffset] '2026-09-15T22:05:00Z'
                    AssignedTo = [pscustomobject]@{ DisplayName = 'CustomerAlpha' }
                }
            })
        Mock Invoke-AdoTestBatch {
            $ids = @(($Body | ConvertFrom-Json).ids)
            [pscustomobject]@{ Content = '{"value":[' + ((@($ids | ForEach-Object {
                                '{"id":' + $_ + ',"fields":{"System.CreatedDate":"2026-09-15T22:05:00Z","System.AssignedTo":{"displayName":"CustomerAlpha"}}}'
                            })) -join ',') + ']}' }
        }
        $lines = @(. (Get-LiveSection 'TestFailures' 'BUG_FIELDS_PRESENT_AND_SHAPED'))
        Should -Invoke Invoke-AdoTestBatch -Times 2 -Exactly
        Should -Invoke Invoke-AdoTestBatch -Times 1 -Exactly -ParameterFilter { @(($Body | ConvertFrom-Json).ids).Count -eq 200 }
        Should -Invoke Invoke-AdoTestBatch -Times 1 -Exactly -ParameterFilter { @(($Body | ConvertFrom-Json).ids).Count -eq 1 }
        ($lines -join "`n") | Should -Match 'REQUESTED=201 COUNTED=201'
        ($lines -join "`n") | Should -Match '(?m)^PASS V-37 BUG_FIELDS_PRESENT_AND_SHAPED$'
    }

    # A profile without a default project is a missing input, as in every other live script.
    It 'SMOKE-2 reports a missing input as INCONCLUSIVE and anything else as FAIL' {
        $checkStates = [System.Collections.Generic.List[string]]::new()
        . (Get-LiveDefinitions 'Smoke')
        @(Invoke-AdoLiveStep 'SMOKE-2' { throw 'PROFILE_DEFAULT_PROJECT_REQUIRED' }) | Should -Be @('INCONCLUSIVE SMOKE-2 PROFILE_DEFAULT_PROJECT_REQUIRED')
        $script:stepPassed | Should -BeFalse
        @(Invoke-AdoLiveStep 'SMOKE-2' { throw 'CONNECTION_FAILED' }) | Should -Be @('FAIL SMOKE-2 CONNECTION_FAILED')
        @(Invoke-AdoLiveStep 'SMOKE-2' { 'CONNECT_TEST_PROJECTS' }) | Should -Be @('PASS SMOKE-2 CONNECT_TEST_PROJECTS')
        @($checkStates) | Should -Be @('INCONCLUSIVE', 'FAIL', 'PASS')
    }

    It 'F16 omits dynamic keys from smoke exception paths too' {
        Get-AdoSafeJsonPath '$.customFields[0].value.CustomerZeta' | Should -Be 'UNPRINTABLE'
        Get-AdoSafeJsonPath '$.fields.SystemTitle' | Should -Be 'UNPRINTABLE'
        Get-AdoSafeJsonPath '$.value[0].testRun.id' | Should -Be '$.value[0].testRun.id'
    }

    # V-31: the fields and relation attributes that Export-AdoTestCase -IncludeDetail reads.
    It 'V-31 reads the shapes of a Test Case as <Problem> with counts only' -TestCases @(
        @{ Problem = ''; Fields = '"System.Description":"<p>CustomerAlpha</p>","System.Tags":"CustomerBeta","System.CreatedBy":{"displayName":"CustomerGamma"},"System.CreatedDate":"2026-08-01T08:30:00Z"' }
        @{ Problem = ''; Fields = '"System.CreatedBy":"CustomerGamma","System.CreatedDate":"2026-08-01T08:30:00Z"' }
        @{ Problem = 'FIELD_NOT_TEXT'; Fields = '"System.Description":{"html":"CustomerAlpha"},"System.CreatedBy":"CustomerGamma","System.CreatedDate":"2026-08-01T08:30:00Z"' }
        @{ Problem = 'CREATED_BY_SHAPE'; Fields = '"System.CreatedBy":{"name":"CustomerGamma"},"System.CreatedDate":"2026-08-01T08:30:00Z"' }
        @{ Problem = 'CREATED_BY_MISSING'; Fields = '"System.CreatedDate":"2026-08-01T08:30:00Z"' }
        @{ Problem = 'CREATED_DATE_SHAPE'; Fields = '"System.CreatedBy":"CustomerGamma","System.CreatedDate":"CustomerDelta"' }
    ) {
        param($Problem, $Fields)
        $item = ('{"id":10,"fields":{' + $Fields + '},"relations":[' +
            '{"rel":"Microsoft.VSTS.Common.TestedBy-Reverse","url":"https://ado.example.test/Collection/_apis/wit/workItems/3050","attributes":{"name":"CustomerEpsilon"}},' +
            '{"rel":"Microsoft.VSTS.Common.TestedBy-Reverse","url":"https://ado.example.test/Collection/_apis/wit/workItems/3050","attributes":{"name":"CustomerEpsilon"}},' +
            '{"rel":"System.LinkTypes.Related","url":"https://ado.example.test/Collection/_apis/wit/workItems/3001","attributes":{}},' +
            '{"rel":"System.LinkTypes.Related","url":"https://ado.example.test/Collection/_apis/wit/workItems/10","attributes":{"name":"CustomerZeta"}},' +
            '{"rel":"Hyperlink","url":"https://ado.example.test/CustomerEta","attributes":{}},' +
            '{"rel":"ArtifactLink","url":"vstfs:///CustomerTheta/1","attributes":{"name":"CustomerIota"}},' +
            '{"rel":"AttachedFile","url":"https://ado.example.test/CustomerKappa","attributes":{"name":"CustomerLambda","resourceSize":20480}}]}') | ConvertFrom-Json
        $evidence = Get-AdoLiveTestCaseDetailEvidence -Item $item -TestCaseId 10
        @($evidence.Problems) | Should -Be @(if ($Problem) { $Problem })
        # A repeated link and a link to the Test Case itself are not counted, as the toolkit does not show them.
        $evidence.Counts.LINKS | Should -Be 2
        $evidence.Counts.LINKS_WITH_NAME | Should -Be 1
        $evidence.Counts.HYPERLINKS | Should -Be 1
        $evidence.Counts.ATTACHMENTS | Should -Be 1
        $evidence.Counts.ATTACHMENTS_WITH_SIZE | Should -Be 1
        (Format-AdoLiveCounts $evidence.Counts) + ' ' + ($evidence.Problems -join ' ') | Should -Not -Match 'Customer'
    }

    It 'V-31 names an unreadable Test Case and an attachment that the toolkit would skip' {
        @((Get-AdoLiveTestCaseDetailEvidence -Item $null -TestCaseId 10).Problems) | Should -Be @('TESTCASE_UNREADABLE')
        $item = '{"id":10,"fields":{"System.CreatedBy":"CustomerGamma","System.CreatedDate":"2026-08-01T08:30:00Z"},"relations":[{"rel":"AttachedFile","url":"https://ado.example.test/CustomerKappa","attributes":{}},{"rel":"Hyperlink","attributes":{}}]}' | ConvertFrom-Json
        @((Get-AdoLiveTestCaseDetailEvidence -Item $item -TestCaseId 10).Problems) | Should -Be @('ATTACHMENT_WITHOUT_NAME', 'HYPERLINK_WITHOUT_URL')
    }

    It 'V-31 reports <Verdict> for the installed export and the batch read' -TestCases @(
        @{ Verdict = 'PASS V-31 FIELDS_AND_RELATIONS_AS_ASSUMED'; Description = '"System.Description":"CustomerAlpha",'; Name = '"name":"CustomerEpsilon"'; Links = 1; Codes = @('UnresolvedLinkedWorkItem') }
        @{ Verdict = 'INCONCLUSIVE V-31 NO_DESCRIPTION'; Description = ''; Name = '"name":"CustomerEpsilon"'; Links = 1; Codes = @() }
        @{ Verdict = 'INCONCLUSIVE V-31 NO_NAMED_WORKITEM_LINK'; Description = '"System.Description":"CustomerAlpha",'; Name = ''; Links = 1; Codes = @() }
        @{ Verdict = 'FAIL V-31 MODULE_LINK_COUNT_DIFFERS'; Description = '"System.Description":"CustomerAlpha",'; Name = '"name":"CustomerEpsilon"'; Links = 0; Codes = @() }
        @{ Verdict = 'FAIL V-31 MODULE_LOOKUP_DEGRADED'; Description = '"System.Description":"CustomerAlpha",'; Name = '"name":"CustomerEpsilon"'; Links = 1; Codes = @('LinkedWorkItemsUnavailable') }
        @{ Verdict = 'FAIL V-31 FIELD_NOT_TEXT'; Description = '"System.Description":17,'; Name = '"name":"CustomerEpsilon"'; Links = 1; Codes = @() }
    ) {
        param($Verdict, $Description, $Name, $Links, $Codes)
        $collectionBase = 'https://ado.example.test/Collection'
        $caseId = 10
        $caseText = '10'
        $rendered = [pscustomobject]@{ Links = $Links; Points = 0; Codes = $Codes }
        Mock Invoke-AdoLivePost {
            ('{"value":[{"id":10,"fields":{' + $Description + '"System.CreatedBy":"CustomerGamma","System.CreatedDate":"2026-08-01T08:30:00Z"},"relations":[' +
            '{"rel":"System.LinkTypes.Related","url":"https://ado.example.test/Collection/_apis/wit/workItems/3001","attributes":{' + $Name + '}}]}]}') | ConvertFrom-Json
        }
        $text = @(. (Get-LiveSection 'TestCaseDetail' 'FIELDS_AND_RELATIONS_AS_ASSUMED')) -join "`n"
        $text | Should -Match ('^' + [regex]::Escape($Verdict) + ' DESCRIPTION=[01] TAGS=0 AUTOMATED=0 LINKS=1 LINKS_WITH_NAME=[01] HYPERLINKS=0 ATTACHMENTS=0 ATTACHMENTS_WITH_SIZE=0 RENDERED_LINKS=[01]$')
        $text | Should -Not -Match 'Customer'
        Should -Invoke Invoke-AdoLivePost -Exactly -Times 1 -ParameterFilter {
            $Uri -eq 'https://ado.example.test/Collection/_apis/wit/workitemsbatch?api-version=6.0' -and $Body -eq '{"ids":[10],"errorPolicy":"omit","$expand":"relations"}'
        }
    }

    # V-32: the test points query, its point shape and its paging.
    It 'V-32 reads the answer of the points query as <Problem>' -TestCases @(
        @{ Problem = ''; Points = 2; Response = '{"points":[{"id":1,"outcome":"CustomerMu","testCase":{"id":"10"},"testPlan":{"id":"812","name":"CustomerNu"},"suite":{"id":"813","name":"CustomerXi"},"configuration":{"name":"CustomerOmicron"},"assignedTo":{"displayName":"CustomerPi"}},{"id":"2","outcome":"Unspecified","url":"https://ado.example.test/Collection/CustomerRho/_apis/test/Plans/812/Suites/814/Points/2","testCase":{"id":10}}]}' }
        @{ Problem = ''; Points = 0; Response = '{"points":[]}' }
        @{ Problem = 'POINTS_ARRAY_MISSING'; Points = 0; Response = '{"value":[]}' }
        @{ Problem = 'POINTS_ARRAY_MISSING'; Points = 0; Response = '{"points":{"count":1}}' }
        @{ Problem = 'POINT_WITHOUT_ID'; Points = 1; Response = '{"points":[{"outcome":"CustomerMu","testCase":{"id":"10"}}]}' }
        @{ Problem = 'POINT_FOR_ANOTHER_CASE'; Points = 1; Response = '{"points":[{"id":1,"testCase":{"id":"11"},"testPlan":{"id":"812"},"suite":{"id":"813"}}]}' }
        @{ Problem = 'POINT_NOT_PLACED'; Points = 1; Response = '{"points":[{"id":1,"testCase":{"id":"10"},"testPlan":{"id":"812"}}]}' }
        @{ Problem = 'OUTCOME_NOT_TEXT'; Points = 1; Response = '{"points":[{"id":1,"outcome":2,"testCase":{"id":"10"},"testPlan":{"id":"812"},"suite":{"id":"813"}}]}' }
    ) {
        param($Problem, $Points, $Response)
        $evidence = Get-AdoLiveTestPointEvidence -Response ($Response | ConvertFrom-Json) -TestCaseId 10
        @($evidence.Problems) | Should -Be @(if ($Problem) { $Problem })
        $evidence.Counts.POINTS | Should -Be $Points
        if ($Points -eq 2) {
            (Format-AdoLiveCounts $evidence.Counts) | Should -Be 'POINTS=2 WITH_PLAN_REFERENCE=1 WITH_SUITE_REFERENCE=1 WITH_NAMES=1 WITH_CONFIGURATION=1 RUN=1 NOT_RUN=1 WITH_TESTER=1'
            @($evidence.Ids) | Should -Be @(1, 2)
        }
        @((Get-AdoLiveTestPointEvidence -Response $null -TestCaseId 10).Problems) | Should -Be @('POINTS_ARRAY_MISSING')
    }

    It 'V-32 calls paging <Expected> for the pages [<First>] and [<Second>]' -TestCases @(
        @{ First = @(1); Second = @(2); Expected = 'HONORED' }
        @{ First = @(1); Second = @(1); Expected = 'SKIP_IGNORED' }
        @{ First = @(1, 2); Second = @(2); Expected = 'TOP_IGNORED' }
        @{ First = @(1); Second = @(); Expected = 'PAGE_EMPTY' }
    ) {
        param($First, $Second, $Expected)
        Get-AdoLivePagingEvidence -First ([int[]] $First) -Second ([int[]] $Second) | Should -Be $Expected
    }

    It 'V-32 reports <Verdict> for the installed export and the query' -TestCases @(
        @{ Verdict = 'PASS V-32 POINTS_QUERY_AS_ASSUMED'; Count = 2; Rendered = 2; Codes = @(); SkipHonored = $true; Requests = 3 }
        @{ Verdict = 'FAIL V-32 PAGING_SKIP_IGNORED'; Count = 2; Rendered = 2; Codes = @(); SkipHonored = $false; Requests = 3 }
        @{ Verdict = 'INCONCLUSIVE V-32 TWO_POINTS_REQUIRED_FOR_PAGING'; Count = 1; Rendered = 1; Codes = @(); SkipHonored = $true; Requests = 1 }
        @{ Verdict = 'INCONCLUSIVE V-32 NO_POINTS_FOR_CASE'; Count = 0; Rendered = 0; Codes = @(); SkipHonored = $true; Requests = 1 }
        @{ Verdict = 'FAIL V-32 MODULE_POINT_COUNT_DIFFERS'; Count = 2; Rendered = 1; Codes = @(); SkipHonored = $true; Requests = 3 }
        @{ Verdict = 'FAIL V-32 MODULE_LOOKUP_DEGRADED'; Count = 2; Rendered = 0; Codes = @('TestPointsUnavailable'); SkipHonored = $true; Requests = 3 }
    ) {
        param($Verdict, $Count, $Rendered, $Codes, $SkipHonored, $Requests)
        $pointsBase = 'https://ado.example.test/Collection/Project/_apis/test/points?api-version=6.0-preview.2'
        $caseId = 10
        $caseText = '10'
        $rendered = [pscustomobject]@{ Links = 0; Points = $Rendered; Codes = $Codes }
        Mock Invoke-AdoLivePost {
            param($Uri, $Body)
            $all = @(for ($index = 1; $index -le $Count; $index++) {
                    '{"id":' + $index + ',"outcome":"CustomerMu","testCase":{"id":"10"},"testPlan":{"id":"812"},"suite":{"id":"813"}}' })
            $page = if ($Uri -match 'top=1&%24skip=([01])$') { @($all | Select-Object -Skip $(if ($SkipHonored) { [int] $Matches[1] } else { 0 }) -First 1) } else { $all }
            ('{"points":[' + ($page -join ',') + ']}') | ConvertFrom-Json
        }
        $text = @(. (Get-LiveSection 'TestCaseDetail' 'POINTS_QUERY_AS_ASSUMED')) -join "`n"
        $text | Should -Match ('^' + [regex]::Escape($Verdict) + ' POINTS=[0-2] ')
        $text | Should -Match ' RENDERED_POINTS=[0-2] PAGING=(HONORED|SKIP_IGNORED|UNOBSERVED)$'
        $text | Should -Not -Match 'Customer'
        Should -Invoke Invoke-AdoLivePost -Exactly -Times $Requests -ParameterFilter { $Body -eq '{"pointsFilter":{"testcaseIds":[10]}}' }
    }

    It 'V-32 reports a rejected query by its status only' {
        $pointsBase = 'https://ado.example.test/Collection/Project/_apis/test/points?api-version=6.0-preview.2'
        $caseId = 10
        $caseText = '10'
        $rendered = [pscustomobject]@{ Links = 0; Points = 0; Codes = @('TestPointsUnavailable') }
        Mock Invoke-AdoLivePost {
            $failure = [System.Exception]::new('CustomerSigma')
            $failure | Add-Member -NotePropertyName Response -NotePropertyValue ([pscustomobject]@{ StatusCode = 404 })
            throw $failure
        }
        @(. (Get-LiveSection 'TestCaseDetail' 'POINTS_QUERY_AS_ASSUMED')) -join "`n" | Should -Be 'FAIL V-32 QUERY_REJECTED STATUS=404'
    }
}

# The probes for the next version (tests/Live/Probes.Live.ps1): pure helpers and single validation
# blocks, with synthetic answers. Customer* stands for server text that must never be printed.
Describe 'Live probes with synthetic data only' {
    BeforeEach {
        $printed = [System.Collections.Generic.List[string]]::new()
        $collectionBase = 'https://ado.example.test/Collection'
        $projectBase = 'https://ado.example.test/Collection/Project'
        $connection = [pscustomobject]@{ CollectionUri = [uri] 'https://ado.example.test/Collection'; RequestTimeoutSeconds = 30 }
        $details = [uri]::EscapeDataString('Iterations,WorkItems,SubResults')
        $hasBuild = $true
        $hasRerun = $true
        $rerunId = 402
        $runs = @([pscustomobject]@{ id = 201; runStatistics = @([pscustomobject]@{ outcome = 'Failed'; count = 2 }) })
        Mock Invoke-WebRequest { throw 'Network forbidden in offline tests.' }
        Mock Invoke-RestMethod { throw 'Network forbidden in offline tests.' }
        function New-Location {
            param([string] $Name, [string] $Route, [string] $Released = '6.0', [string] $Maximum = '6.1')
            [pscustomobject]@{ id = [guid]::NewGuid(); area = 'Test'; resourceName = $Name; routeTemplate = $Route; releasedVersion = $Released; maxVersion = $Maximum }
        }
        function ConvertTo-Gzip {
            param([string] $Text)
            $buffer = [System.IO.MemoryStream]::new()
            $zip = [System.IO.Compression.GZipStream]::new($buffer, [System.IO.Compression.CompressionLevel]::Optimal, $true)
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
            $zip.Write($bytes, 0, $bytes.Length)
            $zip.Dispose()
            , $buffer.ToArray()
        }
    }

    It 'V-28 names each allowlisted route by its own name, with versions as numbers only' {
        $locations = @(
            (New-Location 'ResultSummaryByBuild' '{project}/_apis/{area}/CustomerAlpha' '6.0' '6.1')
            (New-Location 'Results' '{project}/_apis/{area}/Runs/{runId}/{resource}/{testCaseResultId}' 'CustomerBeta' '6.0')
            (New-Location 'TestHistory' '{project}/_apis/{area}/Results/testhistory' '6.0.12345' '6.0'))
        $routes = @(Get-AdoLiveRouteEvidence -Location $locations -Allowlist (Get-AdoProbeRoute))
        @($routes | ForEach-Object { $_.Name + ' ' + $_.State + ' ' + $_.Released + ' ' + $_.Maximum }) | Should -Be @(
            'RESULT_SUMMARY_BY_BUILD PRESENT 6.0 6.1', 'RESULTS_BY_BUILD ABSENT UNKNOWN UNKNOWN',
            # The per-run Results route is not the query route.
            'RESULTS_QUERY ABSENT UNKNOWN UNKNOWN', 'TEST_HISTORY PRESENT 6.0 6.0')
        ConvertTo-AdoLiveVersion 'CustomerGamma' | Should -Be 'UNKNOWN'
        ConvertTo-AdoLiveVersion $null | Should -Be 'UNKNOWN'
    }

    It 'V-28 reports <Verdict> with route notes and counts only' -TestCases @(
        @{ Names = @('ResultSummaryByBuild', 'ResultsByBuild', 'Results', 'TestHistory'); Verdict = 'PASS V-28 ROUTES_PRESENT LOCATIONS=4 ABSENT=0' }
        @{ Names = @('ResultSummaryByBuild', 'Results'); Verdict = 'FAIL V-28 ROUTES_ABSENT LOCATIONS=2 ABSENT=2' }
        @{ Names = @(); Verdict = 'INCONCLUSIVE V-28 NO_LOCATIONS_LISTED LOCATIONS=0 ABSENT=4' }
    ) {
        param($Names, $Verdict)
        Mock Get-AdoProbeJson {
            param($Uri, $Method)
            [pscustomobject]@{ count = $Names.Count; value = @($Names | ForEach-Object { New-Location $_ ('{project}/_apis/{area}/' + $_ + '/CustomerDelta') }) }
        }
        $lines = @(. (Get-LiveSection 'Probes' 'ROUTES_ABSENT'))
        $lines.Count | Should -Be 5
        @($lines | Select-Object -First 4 | ForEach-Object { $_ -replace ' RELEASED=.*$' }) | Should -Be @(
            ('NOTE V-28 RESULT_SUMMARY_BY_BUILD ' + $(if ($Names -contains 'ResultSummaryByBuild') { 'PRESENT' } else { 'ABSENT' })),
            ('NOTE V-28 RESULTS_BY_BUILD ' + $(if ($Names -contains 'ResultsByBuild') { 'PRESENT' } else { 'ABSENT' })),
            ('NOTE V-28 RESULTS_QUERY ' + $(if ($Names -contains 'Results') { 'PRESENT' } else { 'ABSENT' })),
            ('NOTE V-28 TEST_HISTORY ' + $(if ($Names -contains 'TestHistory') { 'PRESENT' } else { 'ABSENT' })))
        $lines[4] | Should -Be $Verdict
        $lines -join "`n" | Should -Not -Match 'Customer'
        Should -Invoke Get-AdoProbeJson -Exactly -Times 1 -ParameterFilter { $Uri -eq 'https://ado.example.test/Collection/_apis/test' -and $Method -eq 'Options' }
    }

    It 'V-34 compares the fields read, the sub-results, the iterations and the text lengths' {
        $detail = [pscustomobject]@{ id = 1; outcome = 'Failed'; errorMessage = 'CustomerEpsilon'; stackTrace = 'CustomerZeta'; failingSince = [pscustomobject]@{ build = 1 }
            subResults = @([pscustomobject]@{ id = 1 }, [pscustomobject]@{ id = 2 }); iterationDetails = @() }
        $same = [pscustomobject]@{ id = 1; outcome = 'Failed'; errorMessage = 'CustomerEpsilon'; stackTrace = 'CustomerZeta'; failingSince = $null
            subResults = @([pscustomobject]@{ id = 1 }, [pscustomobject]@{ id = 2 }); iterationDetails = @(); url = 'CustomerEta' }
        $agreement = Get-AdoLiveListedDetailAgreement -Listed $same -Detail $detail
        @($agreement.Fields, $agreement.SubResults, $agreement.Iterations, $agreement.Text) | Should -Be @($true, $true, $true, $true)
        # A field left out, one sub-result fewer and a message cut short.
        $short = [pscustomobject]@{ id = 1; outcome = 'Failed'; errorMessage = 'Customer'; stackTrace = 'CustomerZeta'; subResults = @([pscustomobject]@{ id = 1 }) }
        $agreement = Get-AdoLiveListedDetailAgreement -Listed $short -Detail $detail
        @($agreement.Fields, $agreement.SubResults, $agreement.Iterations, $agreement.Text) | Should -Be @($false, $false, $true, $false)
    }

    It 'V-34 reports <Verdict> with counts only' -TestCases @(
        @{ Cut = $false; Verdict = 'PASS V-34 LISTED_DETAILS_AGREE LISTED=3 COMPARED=2 FIELDS=2 SUB_RESULTS=2 ITERATIONS=2 TEXT=2 MESSAGES_AT_4000=0' }
        @{ Cut = $true; Verdict = 'FAIL V-34 LISTED_DETAILS_DIFFER LISTED=3 COMPARED=2 FIELDS=2 SUB_RESULTS=2 ITERATIONS=2 TEXT=0 MESSAGES_AT_4000=2' }
    ) {
        param($Cut, $Verdict)
        Mock Get-AdoProbeJson {
            param($Uri)
            $long = 'C' * 4100
            if ($Uri -match '/results/[0-9]+\?') { return [pscustomobject]@{ id = 1; outcome = 'Failed'; errorMessage = $long; stackTrace = 'CustomerTheta' } }
            $listed = if ($Cut) { $long.Substring(0, 4000) } else { $long }
            [pscustomobject]@{ value = @(
                    [pscustomobject]@{ id = 1; outcome = 'Failed'; errorMessage = $listed; stackTrace = 'CustomerTheta' }
                    [pscustomobject]@{ id = 2; outcome = 'Passed'; errorMessage = $null; stackTrace = $null }
                    [pscustomobject]@{ id = 3; outcome = 'Error'; errorMessage = $listed; stackTrace = 'CustomerTheta' }) }
        }
        $lines = @(. (Get-LiveSection 'Probes' 'LISTED_DETAILS_DIFFER'))
        $lines | Should -Be @($Verdict)
        Should -Invoke Get-AdoProbeJson -Exactly -Times 1 -ParameterFilter { $Uri -match '/Runs/201/results\?api-version=6\.0&detailsToInclude=Iterations%2CWorkItems%2CSubResults&%24top=200$' }
        Should -Invoke Get-AdoProbeJson -Exactly -Times 2 -ParameterFilter { $Uri -match '/Runs/201/results/[13]\?api-version=6\.0&detailsToInclude=Iterations%2CWorkItems%2CSubResults$' }
    }

    It 'V-35 decodes a gzip body and measures it as a percentage' {
        $text = '{"value":[' + ((1..200 | ForEach-Object { '{"id":' + $_ + ',"outcome":"Passed"}' }) -join ',') + ']}'
        $bytes = ConvertTo-Gzip $text
        Get-AdoLiveDecodedLength -Bytes $bytes -ContentEncoding 'gzip' | Should -Be ([System.Text.Encoding]::UTF8.GetByteCount($text))
        Get-AdoLiveDecodedLength -Bytes ([byte[]] @(1, 2, 3)) -ContentEncoding '' | Should -Be 3
        $evidence = Get-AdoLiveCompressionEvidence -ContentEncoding 'gzip' -WireBytes 25 -DecodedBytes 100
        @($evidence.State, $evidence.Percent) | Should -Be @('COMPRESSED', 25)
        (Get-AdoLiveCompressionEvidence -ContentEncoding $null -WireBytes 100 -DecodedBytes 100).State | Should -Be 'UNCOMPRESSED'
    }

    It 'V-35 reports <Verdict> from the body as it came over the wire' -TestCases @(
        @{ Encoding = 'gzip'; Size = 200; Verdict = '^PASS V-35 COMPRESSED PERCENT=[0-9]+ DECODED_BYTES=[0-9]+$' }
        @{ Encoding = ''; Size = 200; Verdict = '^FAIL V-35 UNCOMPRESSED PERCENT=100 DECODED_BYTES=[0-9]+$' }
        @{ Encoding = ''; Size = 2; Verdict = '^INCONCLUSIVE V-35 PAGE_TOO_SMALL PERCENT=100 DECODED_BYTES=[0-9]+$' }
    ) {
        param($Encoding, $Size, $Verdict)
        Mock Invoke-AdoProbeRawRequest {
            $text = '{"value":[' + ((1..$Size | ForEach-Object { '{"id":' + $_ + ',"outcome":"Passed"}' }) -join ',') + ']}'
            [pscustomobject]@{ Bytes = $(if ($Encoding -eq 'gzip') { ConvertTo-Gzip $text } else { [System.Text.Encoding]::UTF8.GetBytes($text) }); ContentEncoding = $Encoding }
        }
        @(. (Get-LiveSection 'Probes' 'PAGE_TOO_SMALL')) -join "`n" | Should -Match $Verdict
        Should -Invoke Invoke-AdoProbeRawRequest -Exactly -Times 1 -ParameterFilter { $Uri -match '/Runs/201/results\?api-version=6\.0&detailsToInclude=None&%24top=1000&%24skip=0$' }
    }

    It 'V-36 tells OVERLAP from DISJOINT and needs every sub-result attachment to pass' {
        $full = Get-AdoLiveAttachmentOverlap -Result @(61, 62, 63) -SubResult @(62, 63, 63)
        @($full.State, $full.Complete, (Format-AdoLiveCounts $full.Counts)) | Should -Be @('OVERLAP', $true, 'RESULT=3 SUB_RESULTS=2 SHARED=2')
        $part = Get-AdoLiveAttachmentOverlap -Result @(61, 62) -SubResult @(62, 64)
        @($part.State, $part.Complete) | Should -Be @('OVERLAP', $false)
        $none = Get-AdoLiveAttachmentOverlap -Result @(61) -SubResult @(64)
        @($none.State, $none.Complete) | Should -Be @('DISJOINT', $false)
    }

    It 'V-36 reports <Verdict> with counts only' -TestCases @(
        @{ Own = @(71, 72); Sub = @(72); Verdict = 'PASS V-36 OVERLAP RESULT=2 SUB_RESULTS=1 SHARED=1' }
        @{ Own = @(71); Sub = @(72); Verdict = 'FAIL V-36 DISJOINT RESULT=1 SUB_RESULTS=1 SHARED=0' }
        @{ Own = @(71); Sub = @(); Verdict = 'INCONCLUSIVE V-36 NO_SUB_RESULT_ATTACHMENTS RERUNS=1' }
    ) {
        param($Own, $Sub, $Verdict)
        Mock Get-AdoProbeRun { @([pscustomobject]@{ id = 301; name = 'CustomerIota' }) }
        Mock Get-AdoProbeJson {
            param($Uri)
            if ($Uri -match 'testSubResultId=') { return [pscustomobject]@{ value = @($Sub | ForEach-Object { [pscustomobject]@{ id = $_; fileName = 'CustomerKappa' } }) } }
            if ($Uri -match '/attachments\?') { return [pscustomobject]@{ value = @($Own | ForEach-Object { [pscustomobject]@{ id = $_; fileName = 'CustomerLambda' } }) } }
            if ($Uri -match '/results/5\?') { return [pscustomobject]@{ id = 5; resultGroupType = 'Rerun'; subResults = @([pscustomobject]@{ id = 1 }) } }
            [pscustomobject]@{ value = @([pscustomobject]@{ id = 4; outcome = 'Passed' }, [pscustomobject]@{ id = 5; outcome = 'Passed'; resultGroupType = 'Rerun' }) }
        }
        $lines = @(. (Get-LiveSection 'Probes' 'NO_SUB_RESULT_ATTACHMENTS'))
        $lines | Should -Be @($Verdict)
        Should -Invoke Get-AdoProbeRun -Exactly -Times 1 -ParameterFilter { $BuildId -eq 402 }
        Should -Invoke Get-AdoProbeJson -Exactly -Times 1 -ParameterFilter { $Uri -match '/Runs/301/Results/5/attachments\?api-version=6\.0-preview\.1&testSubResultId=1$' }
    }

    It 'gives every probe one verdict, after its route notes, and leaves out the checks already settled' {
        Get-AdoLivePendingCheck -Id @('V-28', 'V-34', 'V-35', 'V-36') -Line @('NOTE V-28 TEST_HISTORY PRESENT RELEASED=6.0 MAX=6.0', 'PASS V-28 ROUTES_PRESENT LOCATIONS=4 ABSENT=0',
            'FAIL V-34 CHECK_FAILED') | Should -Be @('V-35', 'V-36')
    }
}

# tests/Live/AGENTS.md: one verdict per check ID. S0-9 is one criterion, "Connect-Ado; Test-AdoConnection;
# Get-AdoProject succeeds from a signed installed module", so the signature is an observation before it,
# not a verdict of its own.
Describe 'Live connection check with synthetic data only' {
    It 'gives S0-9 one verdict, after the signature note' -TestCases @(
        @{ Signed = $true; Note = 'NOTE S0-9 SIGNATURE_VALID' }
        @{ Signed = $false; Note = 'NOTE S0-9 UNSIGNED_RELEASE_INSTALLED' }
    ) {
        param($Signed, $Note)
        $installed = [pscustomobject]@{ Signed = $Signed }
        function Connect-Ado { param([Parameter(ValueFromRemainingArguments = $true)] $Rest) }
        function Test-AdoConnection { param([Parameter(ValueFromRemainingArguments = $true)] $Rest) [pscustomobject]@{ Success = $true } }
        function Get-AdoProject { param([Parameter(ValueFromRemainingArguments = $true)] $Rest) }
        $source = (Read-LiveAst 'Connection').Extent.Text
        $start = $source.IndexOf('if ($installed.Signed)', [StringComparison]::Ordinal)
        $end = $source.IndexOf('exit 2', $start, [StringComparison]::Ordinal)
        $lines = @(. ([scriptblock]::Create($source.Substring($start, $end - $start))))

        @($lines | Where-Object { $_ -match '^(PASS|FAIL|INCONCLUSIVE) S0-9( |$)' }) | Should -Be @('PASS S0-9 CONNECT_TEST_PROJECTS')
        $lines[0] | Should -Be $Note
    }
}
