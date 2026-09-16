BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
}
Describe 'Cmdlet cancellation' -Tag 'S0-4' {
    It 'stops an in-flight request within five seconds without a stack trace' {
        $server = Start-FakeAdoServer -Responses @(@{ Block = $true })
        $shell = [powershell]::Create()
        try {
            [void] $shell.AddScript({
                param($Manifest, $Uri)
                Import-Module -Name $Manifest
                Connect-Ado -CollectionUrl $Uri -WarningAction SilentlyContinue | Out-Null
                Get-AdoProject
            }).AddArgument($env:ADOTOOLKIT_MODULE_MANIFEST).AddArgument($server.Uri)
            $pending = $shell.BeginInvoke()
            $deadline = [datetime]::UtcNow.AddSeconds(10)
            while ($server.Requests.Count -eq 0 -and [datetime]::UtcNow -lt $deadline -and -not $pending.IsCompleted) { Start-Sleep -Milliseconds 20 }
            $server.Requests.Count | Should -Be 1
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $shell.Stop()
            $watch.Elapsed.TotalSeconds | Should -BeLessThan 5
            $shell.InvocationStateInfo.State | Should -Be 'Stopped'
            @($shell.Streams.Error | Where-Object { $_.ScriptStackTrace -or $_.Exception.StackTrace }).Count | Should -Be 0
        }
        finally { $shell.Dispose(); Stop-FakeAdoServer -Server $server }
    }
}

