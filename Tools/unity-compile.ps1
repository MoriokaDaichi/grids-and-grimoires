# Headless script-compile check.
# Opens the project in batch mode (which forces a compile) and scans the log
# for C# compiler errors. Exit code 0 = clean, 1 = errors found or Unity failed.
#
# Usage:  pwsh -File Tools\unity-compile.ps1
#
# NOTE: close the Unity Editor first (batch mode needs an exclusive project lock).

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-common.ps1"

Assert-Unity
Assert-EditorClosed

$log = Join-Path $LogDir 'compile.log'
if (Test-Path $log) { Remove-Item $log }

$exit = Invoke-Unity -UnityArgs @() -LogFile $log

$errors = @()
if (Test-Path $log) {
    $errors = Select-String -Path $log -Pattern '\)\s*:\s*error\s+CS\d+' -ErrorAction SilentlyContinue
}

Write-Host ''
if ($errors.Count -gt 0) {
    Write-Host "COMPILE ERRORS ($($errors.Count)):" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  $($_.Line.Trim())" -ForegroundColor Red }
    Write-Host "`nFull log: $log"
    exit 1
}

if ($exit -ne 0) {
    Write-Host "No CS errors matched, but Unity exited with code $exit. Check $log" -ForegroundColor Yellow
    exit 1
}

Write-Host "Compile clean." -ForegroundColor Green
exit 0
