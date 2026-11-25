[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [string]$VmHost = '127.0.0.1',
    [ValidateRange(1,65535)]
    [int]$SshPort = 2227,
    [ValidatePattern('^[a-z_][a-z0-9_-]*$')]
    [string]$User = 'dustline',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$RemoteDirectory = 'dustline-pr7',
    [string]$IdentityFile,
    [string]$KnownHostsFile,
    [switch]$Steam,
    [switch]$Sudo
)

$ErrorActionPreference = 'Stop'
$pr7Root = (Resolve-Path "$PSScriptRoot/..").Path
$pr7Server = Join-Path $pr7Root 'Builds/PR7/LinuxServer/DustlineServer.x86_64'
if (!(Test-Path -LiteralPath $pr7Server -PathType Leaf)) { throw 'Build the Linux Dedicated Server first.' }
$pr7Archive = Join-Path ([IO.Path]::GetTempPath()) ("dustline-pr7-" + [guid]::NewGuid().ToString('N') + '.tar.gz')
$pr7RemoteFile = Split-Path -Leaf $pr7Archive
$pr7Destination = "$User@$VmHost"
$pr7SshOptions = @('-o','BatchMode=yes','-o','StrictHostKeyChecking=yes')
if ($IdentityFile) { $pr7SshOptions += @('-i',(Resolve-Path -LiteralPath $IdentityFile).Path,'-o','IdentitiesOnly=yes') }
if ($KnownHostsFile) {
    $pr7KnownHosts = (Resolve-Path -LiteralPath $KnownHostsFile).Path
    $pr7SshOptions += @('-o',"UserKnownHostsFile=$pr7KnownHosts")
}
try {
    $pr7Files = @('.dockerignore','deploy/Dockerfile','deploy/compose.yaml','deploy/compose.steam.yaml',
        'deploy/.env.example','deploy/entrypoint.sh','scripts/server-compose.sh','scripts/server-start.sh',
        'scripts/server-stop.sh','scripts/server-status.sh','scripts/test-container.sh','Builds/PR7/LinuxServer')
    & tar.exe -czf $pr7Archive -C $pr7Root @pr7Files
    if ($LASTEXITCODE) { throw 'Unable to package deployment files.' }
    & scp.exe -P $SshPort @pr7SshOptions $pr7Archive "${pr7Destination}:$pr7RemoteFile"
    if ($LASTEXITCODE) { throw 'SSH upload failed. Verify the VM host key and account first.' }
    $pr7Flavor = if ($Steam) { 'steam' } else { 'ip' }
    $pr7Privilege = if ($Sudo) { 'sudo ' } else { '' }
    $pr7Tty = if ($Sudo) { @('-t') } else { @() }
    $pr7Command = "mkdir -p '$RemoteDirectory' && tar -xzf '$pr7RemoteFile' -C '$RemoteDirectory' && " +
        "rm -f '$pr7RemoteFile' && cd '$RemoteDirectory' && ${pr7Privilege}sh scripts/server-start.sh '$pr7Flavor'"
    & ssh.exe -p $SshPort @pr7SshOptions @pr7Tty $pr7Destination $pr7Command
    if ($LASTEXITCODE) { throw 'VM deployment failed. Check the Compose output.' }
}
finally {
    if (Test-Path -LiteralPath $pr7Archive) { Remove-Item -LiteralPath $pr7Archive }
}
