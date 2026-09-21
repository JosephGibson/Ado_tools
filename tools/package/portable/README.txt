AdoToolkit for Windows x64

1. Before extracting, right-click the downloaded ZIP, choose Properties, select
   Unblock if it is shown, and click Apply.
2. Extract the entire ZIP to a folder you can write to.
3. Double-click Start-AdoToolkit.cmd. A console opens with AdoToolkit loaded.

PowerShell and its .NET runtime are included. No separate installation,
administrator rights or internet download is needed to start the console.
Your Azure DevOps Server must be reachable when you use commands that contact it.

List commands: Get-Command -Module AdoToolkit
Read help:     Get-Help Connect-Ado -Full
Getting started: https://github.com/JosephGibson/Ado_tools#quick-start

Use this launcher each time. Other PowerShell windows do not automatically use
this copy. Relative report paths start in the extracted folder.

To update, download and extract a new AdoToolkit portable release into a new
folder. Close the old console and use the new launcher. Your saved connection
profiles remain in your Windows user profile. The bundled PowerShell is pinned
and does not update itself; runtime fixes ship with new portable releases.

This release's AdoToolkit scripts and module are unsigned. Organization policies
that require signed code still apply. If Windows blocks a script, close the
console, unblock the original ZIP in Properties, and extract it again.

PowerShell license and third-party notices are included in the runtime folder.
Portable packaging does not install modules globally or change your PATH.
