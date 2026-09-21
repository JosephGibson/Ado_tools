BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST
    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    . (Join-Path $repository 'tools/lib/dependencies.ps1')
    $platy = Get-BuildModule -Name Microsoft.PowerShell.PlatyPS
    if ($null -eq $platy) { throw 'The required PlatyPS version is not installed.' }
    Import-Module -Name $platy.Path
}

Describe 'Command help matches compiled parameters' {
    It 'matches parameter names, types, aliases and binding in <Culture>' -TestCases @(
        @{ Culture = 'en-US' }, @{ Culture = 'fr-CA' }
    ) {
        param($Culture)
        $documents = Get-ChildItem -LiteralPath (Join-Path $repository "docs/commands/$Culture") -Filter '*.md' -File
        $issues = [Collections.Generic.List[string]]::new()
        foreach ($topic in Import-MarkdownCommandHelp -LiteralPath $documents.FullName) {
            $command = Get-Command -Name $topic.Title -Module AdoToolkit
            $properties = @($command.ImplementingType.GetProperties() | Where-Object {
                @($_.GetCustomAttributes([Management.Automation.ParameterAttribute], $true)).Count -gt 0
            })
            $expectedNames = @($properties | ForEach-Object Name)
            if ($command.Parameters.ContainsKey('WhatIf')) { $expectedNames += @('WhatIf', 'Confirm') }
            if ((($expectedNames | Sort-Object) -join ',') -cne (($topic.Parameters | ForEach-Object Name | Sort-Object) -join ',')) {
                $issues.Add("$($topic.Title): parameter names differ")
            }
            foreach ($parameter in $topic.Parameters) {
                $label = "$($topic.Title) -$($parameter.Name)"
                $actual = $command.Parameters[$parameter.Name]
                if ($null -eq $actual) { continue }
                if ([Management.Automation.PSTypeName]::new($parameter.Type).Type -ne $actual.ParameterType) {
                    $issues.Add("${label}: type differs")
                }
                if ($parameter.Name -notin @('WhatIf', 'Confirm') -and
                    (($parameter.Aliases | Sort-Object) -join ',') -cne (($actual.Aliases | Sort-Object) -join ',')) {
                    $issues.Add("${label}: aliases differ")
                }
                $covered = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                foreach ($binding in $parameter.ParameterSets) {
                    $sets = if ($binding.Name -eq '(All)') { @($command.ParameterSets.Name) } else { @($binding.Name) }
                    foreach ($setName in $sets) {
                        [void] $covered.Add($setName)
                        $set = $command.ParameterSets | Where-Object Name -eq $setName
                        $bound = @($set.Parameters | Where-Object Name -eq $parameter.Name)
                        if ($bound.Count -ne 1) { $issues.Add("${label}: unexpected set $setName"); continue }
                        $position = if ($bound[0].Position -lt 0) { 'Named' } else { [string] $bound[0].Position }
                        if ($binding.IsRequired -ne $bound[0].IsMandatory -or $binding.Position -ne $position -or
                            $binding.ValueFromPipeline -ne $bound[0].ValueFromPipeline -or
                            $binding.ValueFromPipelineByPropertyName -ne $bound[0].ValueFromPipelineByPropertyName -or
                            $binding.ValueFromRemainingArguments -ne $bound[0].ValueFromRemainingArguments) {
                            $issues.Add("${label}: binding differs in $setName")
                        }
                    }
                }
                foreach ($set in $command.ParameterSets) {
                    if ($parameter.Name -in $set.Parameters.Name -and -not $covered.Contains($set.Name)) {
                        $issues.Add("${label}: missing set $($set.Name)")
                    }
                }
            }
        }
        $issues -join "`n" | Should -Be ''
    }
}
