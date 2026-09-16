BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
}
Describe 'Packaged compiled help and V-17 fallback' {
    It 'loads all twenty-two complete help topics under <Culture>' -TestCases @(
        @{ Culture = 'en-US'; Expected = 'Selects the connection for this runspace.' },
        @{ Culture = 'fr-CA'; Expected = "Sélectionne la connexion de cet espace d’exécution." },
        @{ Culture = 'fr-FR'; Expected = "Sélectionne la connexion de cet espace d’exécution." },
        @{ Culture = 'fr'; Expected = "Sélectionne la connexion de cet espace d’exécution." }
    ) {
        param($Culture, $Expected)
        $previous = $env:ADOTOOLKIT_HELP_CULTURE
        try {
            $env:ADOTOOLKIT_HELP_CULTURE = $Culture
            $script = @'
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo]::GetCultureInfo($env:ADOTOOLKIT_HELP_CULTURE)
Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST
$topics = foreach ($command in Get-Command -Module AdoToolkit) {
    $help = Get-Help -Name $command.Name -Full
    [pscustomobject]@{
        Name = $command.Name
        Synopsis = [string] $help.Synopsis
        Description = [string] $help.Description[0].Text
        Examples = @($help.Examples.Example).Count
        Parameters = @($help.Parameters.Parameter | Where-Object { $null -ne $_ } | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Description = (@($_.Description | ForEach-Object Text) -join ' ') } })
    }
}
# Escape the native-process transport so Windows console code pages cannot
# transliterate French punctuation before the parent decodes the JSON.
[pscustomobject]@{ Culture = $PSUICulture; Topics = @($topics) } | ConvertTo-Json -Depth 6 -Compress -EscapeHandling EscapeNonAscii
'@
            $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($script))
            $arguments = @('-NoProfile', '-EncodedCommand', $encoded)
            $output = & (Join-Path $PSHOME 'pwsh.exe') @arguments
            $LASTEXITCODE | Should -Be 0
            $result = $output | ConvertFrom-Json
            $result.Culture | Should -Be $Culture
            $result.Topics.Count | Should -Be 22
            ($result.Topics | Where-Object Name -eq 'Connect-Ado').Synopsis | Should -Be $Expected
            foreach ($topic in $result.Topics) {
                $topic.Synopsis | Should -Not -BeNullOrEmpty
                $topic.Description | Should -Not -BeNullOrEmpty
                $topic.Examples | Should -BeGreaterThan 0
                foreach ($parameter in $topic.Parameters) { $parameter.Description | Should -Not -BeNullOrEmpty }
            }
            $package = Split-Path -Parent $env:ADOTOOLKIT_MODULE_MANIFEST
            Test-Path -LiteralPath (Join-Path $package 'fr/AdoToolkit.PowerShell.dll-Help.xml') | Should -BeTrue
            Test-Path -LiteralPath (Join-Path $package 'fr-CA') | Should -BeFalse
            Test-Path -LiteralPath (Join-Path $package 'fr-FR') | Should -BeFalse
        }
        finally { $env:ADOTOOLKIT_HELP_CULTURE = $previous }
    }
}
