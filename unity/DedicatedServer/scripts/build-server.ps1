[CmdletBinding()]
param(
    [string]$UnityPath,
    [ValidateSet('LinuxServer', 'WindowsClient')]
    [string]$Target = 'LinuxServer'
)

$ErrorActionPreference = 'Stop'
$pr7Root = (Resolve-Path "$PSScriptRoot/..").Path
$pr7Version = (Get-Content "$pr7Root/ProjectSettings/ProjectVersion.txt" |
    Select-String '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
if (!$UnityPath) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$pr7Version/Editor/Unity.exe"
}
$UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
$pr7EditorVersion = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
if ($pr7EditorVersion -notlike "$pr7Version*") {
    throw "Expected Unity $pr7Version. Installed editor reports $pr7EditorVersion."
}

$pr7Out = Join-Path $pr7Root "Builds/PR7/$Target"
$pr7Logs = Join-Path $pr7Root 'Builds/PR7/logs'
New-Item -ItemType Directory -Force $pr7Out,$pr7Logs | Out-Null
$pr7Log = Join-Path $pr7Logs "$Target-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
if ($Target -eq 'LinuxServer') {
    $pr7Player = Join-Path $pr7Out 'DustlineServer.x86_64'
    $pr7BuildArgs = '-buildTarget Linux64 -standaloneBuildSubtarget Server'
    $pr7Method = 'BuildLinux'
} else {
    $pr7Player = Join-Path $pr7Out 'Dustline.exe'
    $pr7BuildArgs = '-buildTarget Win64 -standaloneBuildSubtarget Player'
    $pr7Method = 'BuildWindowsClient'
}
$pr7Args = "-batchmode -nographics -quit -projectPath `"$pr7Root`" $pr7BuildArgs " +
    "-executeMethod PredictionDemo.EditorTools.DedicatedServerBuilder.$pr7Method " +
    "--pr7-build-output `"$pr7Player`" -logFile `"$pr7Log`""
$pr7Process = Start-Process -FilePath $UnityPath -ArgumentList $pr7Args -WindowStyle Hidden -PassThru -Wait
if ($pr7Process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $pr7Player) -or
    !(Select-String -LiteralPath $pr7Log -SimpleMatch '[PR7] BUILD_OK' -Quiet)) {
    throw "Unity build failed (process $($pr7Process.ExitCode)). See $pr7Log"
}
Write-Output $pr7Player
Write-Output $pr7Log
