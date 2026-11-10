<#
.SYNOPSIS
    Builds the plugin and installs it into a Docker test server, then restarts the server.

.DESCRIPTION
    Copies the staged DLL + meta.json (from build.ps1) into
    docker/.data/<server>/config/plugins/RatingsRecs_<version>/ and restarts the container.
    Compatible with Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER Server
    "10.11", "12" or "all".

.PARAMETER NoBuild
    Reuse the existing artifacts/staging output instead of rebuilding.

.EXAMPLE
    .\docker\deploy.ps1 -Server all
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('10.11', '12', 'all')][string]$Server,
    [switch]$NoBuild
)

. "$PSScriptRoot\common.ps1"

$targets = @($Server)
if ($Server -eq 'all') { $targets = @('10.11', '12') }

foreach ($name in $targets) {
    $srv = Get-TestServer $name
    $staging = Join-Path $script:RepoRoot ('artifacts/staging/' + $srv.BuildId)

    if (-not $NoBuild) {
        & (Join-Path $script:RepoRoot 'build.ps1') -Only $srv.BuildId
    }
    if (-not (Test-Path (Join-Path $staging 'meta.json'))) { throw "Nothing staged in $staging; run build.ps1 first" }

    $meta = [System.IO.File]::ReadAllText((Join-Path $staging 'meta.json'), $script:Utf8NoBom) | ConvertFrom-Json
    $pluginsDir = Join-Path $srv.DataDir 'config/plugins'
    New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
    Get-ChildItem -Path $pluginsDir -Directory -Filter 'RatingsRecs_*' | Remove-Item -Recurse -Force
    $dest = Join-Path $pluginsDir ('RatingsRecs_' + $meta.version)
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $staging '*') $dest

    Write-Host "==> Installed $($meta.version) (targetAbi $($meta.targetAbi)) into $dest"
    Invoke-Native docker @('compose', '-f', $srv.Compose, 'restart')
    Wait-JfReady $srv | Out-Null
    Write-Host "    Jellyfin $name restarted: http://localhost:$($srv.Port)"
}
