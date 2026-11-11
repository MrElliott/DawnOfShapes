<#
.SYNOPSIS
  Builds the Windows player in batch mode.
.PARAMETER RebuildMap
  Regenerate the TestMap scene before building.
.PARAMETER UnityPath
  Override the Unity.exe path. Defaults to the Hub install matching ProjectVersion.txt.
#>
param(
    [switch]$RebuildMap,
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot

if (-not $UnityPath) {
    $versionLine = Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:'
    $version = ($versionLine -split ':\s*')[1].Trim()
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityPath)) { throw "Unity not found at $UnityPath" }

$logDir = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Force $logDir | Out-Null

function Invoke-Unity([string]$method, [string]$logName) {
    $log = Join-Path $logDir $logName
    Write-Host "Running $method (log: $log)"
    $unityArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$projectPath`"",
              '-executeMethod', $method, '-logFile', "`"$log`"")
    $p = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -NoNewWindow -PassThru
    $null = $p.Handle  # cache the handle so ExitCode is available after exit
    # WaitForExit on the process (not Start-Process -Wait) so lingering child processes don't block us.
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) {
        Get-Content $log -Tail 40 | Write-Host
        throw "$method failed with exit code $($p.ExitCode). See $log"
    }
}

if ($RebuildMap -or -not (Test-Path (Join-Path $projectPath 'Assets/_Project/Scenes/TestMap.unity'))) {
    Invoke-Unity 'DawnOfShapes.EditorTools.TestMapBuilder.BuildFromCommandLine' 'build-testmap.log'
}

Invoke-Unity 'DawnOfShapes.EditorTools.BuildScript.BuildWindows' 'build-windows.log'
Write-Host "Build complete: $(Join-Path $projectPath 'Builds/Windows/DawnOfShapes.exe')"
