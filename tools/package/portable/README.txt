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

To update, run Update-AdoToolkit in the console. It installs the newest release
from GitHub in a new folder next to this one and does not change this one. Close
this console, start Start-AdoToolkit.cmd in the new folder, and delete this
folder once its console is closed and you have moved out any reports saved in
it. If the command cannot reach GitHub, download the new portable release and
extract it into a new folder instead. Your saved connection profiles remain in
your Windows user profile. The bundled PowerShell is pinned and does not update
itself; runtime fixes ship with new portable releases.

This release's AdoToolkit scripts and module are unsigned. Organization policies
that require signed code still apply. If Windows blocks a script, close the
console, unblock the original ZIP in Properties, and extract it again.

PowerShell license and third-party notices are included in the runtime folder.
Portable packaging does not install modules globally or change your PATH.
