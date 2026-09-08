# Shared config/helpers for Unity batch-mode scripts.
# Dot-source this from the other scripts: . "$PSScriptRoot\unity-common.ps1"

$UnityExe    = 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe'
$ProjectPath = Split-Path -Parent $PSScriptRoot   # repo root (Tools/..)
$LogDir      = Join-Path $ProjectPath 'Logs'

function Assert-Unity {
    if (-not (Test-Path $UnityExe)) {
        throw "Unity not found at: $UnityExe  (edit Tools\unity-common.ps1 if the version changed)"
    }
    if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }
}

function Assert-EditorClosed {
    # Batch mode cannot open a project that the Editor already has locked.
    $lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (Test-Path $lock) {
        $held = $true
        try { $fs = [IO.File]::Open($lock,'Open','ReadWrite','None'); $fs.Close(); $held = $false } catch {}
        if ($held) {
            throw "The Unity Editor appears to have this project open (Temp\UnityLockfile is locked). Close it, or use the Unity MCP path instead of batch mode."
        }
    }
}

function Invoke-Unity {
    param([string[]]$UnityArgs, [string]$LogFile)

    # -runTests must NOT be combined with -quit: Unity 6 quits before the test
    # runner starts, producing no results file. -runTests exits on its own.
    $base = @('-batchmode', '-nographics')
    if ($UnityArgs -notcontains '-runTests') { $base += '-quit' }

    $allArgs = $base + @(
        '-projectPath', $ProjectPath,
        '-logFile', $LogFile
    ) + $UnityArgs

    Write-Host "> Unity $($allArgs -join ' ')" -ForegroundColor DarkGray
    $p = Start-Process -FilePath $UnityExe -ArgumentList $allArgs -PassThru -Wait -NoNewWindow
    return $p.ExitCode
}
