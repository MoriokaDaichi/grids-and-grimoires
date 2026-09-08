# Headless test run via the Unity Test Framework.
# Exit code mirrors Unity's: 0 = all passed, non-zero = failures or run error.
#
# Usage:
#   pwsh -File Tools\unity-tests.ps1                 # EditMode (default)
#   pwsh -File Tools\unity-tests.ps1 -Platform PlayMode
#   pwsh -File Tools\unity-tests.ps1 -Filter Grids.Tests.MagicGridManagerTests
#
# NOTE: close the Unity Editor first (batch mode needs an exclusive project lock).

param(
    [ValidateSet('EditMode','PlayMode')]
    [string]$Platform = 'EditMode',
    [string]$Filter
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-common.ps1"

Assert-Unity
Assert-EditorClosed

$log     = Join-Path $LogDir "tests-$Platform.log"
$results = Join-Path $LogDir "tests-$Platform-results.xml"
foreach ($f in @($log, $results)) { if (Test-Path $f) { Remove-Item $f } }

$targs = @('-runTests', '-testPlatform', $Platform, '-testResults', $results)
if ($Filter) { $targs += @('-testFilter', $Filter) }

$exit = Invoke-Unity -UnityArgs $targs -LogFile $log

Write-Host ''
if (Test-Path $results) {
    [xml]$xml = Get-Content $results
    $tr = $xml.'test-run'
    if ($tr) {
        Write-Host ("Tests: {0} total  {1} passed  {2} failed  {3} skipped" -f `
            $tr.total, $tr.passed, $tr.failed, $tr.skipped) -ForegroundColor Cyan
        if ([int]$tr.failed -gt 0) {
            $xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
                Write-Host "  FAIL  $($_.fullname)" -ForegroundColor Red
                if ($_.failure.message) { Write-Host "        $($_.failure.message.Trim())" -ForegroundColor DarkRed }
            }
        }
    }
    Write-Host "`nResults XML: $results"
} else {
    Write-Host "No results file produced - the run likely failed to start. Check $log" -ForegroundColor Yellow
}

Write-Host "Full log: $log"
exit $exit
