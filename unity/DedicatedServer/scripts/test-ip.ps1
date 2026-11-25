[CmdletBinding()]
param(
    [string]$ClientPath,
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [string]$ServerAddress = '127.0.0.1',
    [ValidateRange(1,65535)]
    [int]$Port = 7777,
    [Parameter(Mandatory)]
    [string]$EvidenceDirectory
)

$ErrorActionPreference = 'Stop'
$pr7Root = (Resolve-Path "$PSScriptRoot/..").Path
if (!$ClientPath) { $ClientPath = "$pr7Root/Builds/PR7/WindowsClient/Dustline.exe" }
$ClientPath = (Resolve-Path -LiteralPath $ClientPath).Path
$pr7Evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $pr7Evidence) { throw 'Choose a new evidence directory for this run.' }
New-Item -ItemType Directory -Path $pr7Evidence | Out-Null
$pr7Capture = Join-Path ([IO.Path]::GetTempPath()) ("dustline-pr7-capture-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $pr7Capture | Out-Null
$pr7Clients = @()
try {
    foreach ($name in 'A','B') {
        $pr7Log = "$pr7Evidence/client-$name.log"
        $pr7Png = "$pr7Capture/client-$name.png"
        $pr7State = "$pr7Evidence/client-$name.state.txt"
        $pr7Extra = if ($name -eq 'A') { '--pr7-rejoin-smoke' } else { '' }
        $pr7Duration = if ($name -eq 'A') { 26 } else { 34 }
        $pr7Args = "-screen-fullscreen 0 -screen-width 1024 -screen-height 576 -logFile `"$pr7Log`" " +
            "--pr4-client --pr6-address $ServerAddress --pr4-port $Port --pr6-wait-players 2 " +
            "--pr4-auto --pr4-auto-fire --fix-case pr7 --fix-state `"$pr7State`" " +
            "--pr4-screenshot `"$pr7Png`" --pr4-screenshot-at 2 --pr4-exit-after $pr7Duration $pr7Extra"
        $pr7Clients += Start-Process -FilePath $ClientPath -ArgumentList $pr7Args -PassThru
    }
    foreach ($client in $pr7Clients) {
        if (!$client.WaitForExit(125000) -or $client.ExitCode -ne 0) {
            throw "Client failed or timed out. See $pr7Evidence."
        }
    }
    foreach ($name in 'A','B') {
        $pr7Log = "$pr7Evidence/client-$name.log"
        $pr7Png = "$pr7Capture/client-$name.png"
        if (Test-Path -LiteralPath $pr7Png) {
            Move-Item -LiteralPath $pr7Png -Destination "$pr7Evidence/client-$name.png"
        }
        if (!(Select-String -LiteralPath $pr7Log -SimpleMatch '[PR7] JOIN_OK' -Quiet) -or
            !(Select-String -LiteralPath $pr7Log -SimpleMatch '[PR6] SCENARIO_READY' -Quiet) -or
            !(Test-Path -LiteralPath "$pr7Evidence/client-$name.png")) {
            throw "Client $name did not produce joined/two-player evidence."
        }
    }
    if (!(Select-String -LiteralPath "$pr7Evidence/client-A.log" -SimpleMatch '[PR7] SMOKE_REJOIN_OK' -Quiet)) {
        throw 'Client A did not reconnect inside the same game process.'
    }
    Write-Output "Two client joins, two screenshots and same-process rejoin recorded in $pr7Evidence."
}
finally {
    foreach ($client in $pr7Clients) {
        if (!$client.HasExited) { Stop-Process -Id $client.Id }
    }
    foreach ($name in 'A','B') {
        $pr7Png = "$pr7Capture/client-$name.png"
        if (Test-Path -LiteralPath $pr7Png) {
            Move-Item -LiteralPath $pr7Png -Destination "$pr7Evidence/client-$name.png"
        }
    }
    Remove-Item -LiteralPath $pr7Capture
}
