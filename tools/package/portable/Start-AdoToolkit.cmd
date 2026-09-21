@echo off
setlocal DisableDelayedExpansion
if not exist "%~dp0runtime\pwsh.exe" goto missing
if /i "%~1"=="--smoke-test" goto smoke
"%~dp0runtime\pwsh.exe" -NoLogo -NoProfile -ExecutionPolicy RemoteSigned -NoExit -File "%~dp0Start-AdoToolkit.ps1"
exit /b %errorlevel%

:smoke
"%~dp0runtime\pwsh.exe" -NoLogo -NoProfile -ExecutionPolicy RemoteSigned -NonInteractive -File "%~dp0Start-AdoToolkit.ps1" -SmokeTest
exit /b %errorlevel%

:missing
echo AdoToolkit could not find its bundled PowerShell. Extract the entire ZIP before starting it. 1>&2
if /i not "%~1"=="--smoke-test" pause
exit /b 1
