<#
.SYNOPSIS
    Installs the companion plugins (File Transformation, optionally Home Screen Sections and Plugin Pages)
    on the Docker test servers from the IAmParadox27 plugin repository, then restarts the servers.

.PARAMETER Server
    "10.11", "12" or "all".

.PARAMETER Plugins
    Plugin names to install. Default: File Transformation.

.EXAMPLE
    .\docker\install-deps.ps1 -Server all
    .\docker\install-deps.ps1 -Server all -Plugins 'File Transformation','Plugin Pages','Home Screen Sections'
#>
[CmdletBinding()]
param(
    [ValidateSet('10.11', '12', 'all')][string]$Server = 'all',
    [string[]]$Plugins = @('File Transformation')
)

. "$PSScriptRoot\common.ps1"
Set-StrictMode -Off

$repoName = 'IAmParadox27 plugins'
$repoUrl = 'https://www.iamparadox.dev/jellyfin/plugins/manifest.json'
$guids = @{
    'File Transformation' = '5e87cc92-571a-4d8d-8d98-d2d4147f9f90'
    'Home Screen Sections' = 'b8298e01-2697-407a-b44d-aa8dc795e850'
    'Plugin Pages' = '5b6550fa-a014-4f4c-8a2c-59a43680ac6d'
}

$targets = @($Server)
if ($Server -eq 'all') { $targets = @('10.11', '12') }

foreach ($name in $targets) {
    $srv = Get-TestServer $name
    Wait-JfReady $srv | Out-Null
    $token = Get-AdminToken $srv

    $repos = @(Invoke-Jf -Srv $srv -Method Get -Path '/Repositories' -Token $token | ForEach-Object { $_ })
    if (-not ($repos | Where-Object { $_.Url -eq $repoUrl })) {
        Write-Host "==> [$name] adding repository $repoUrl"
        $list = @($repos | ForEach-Object { @{ Name = $_.Name; Url = $_.Url; Enabled = $_.Enabled } })
        $list += @{ Name = $repoName; Url = $repoUrl; Enabled = $true }
        Invoke-Jf -Srv $srv -Method Post -Path '/Repositories' -Token $token -Body $list | Out-Null
    }

    # Loads the catalog (the install endpoint answers 404 until the repository has been read once).
    $deadline = (Get-Date).AddSeconds(60)
    do {
        $catalog = @(Invoke-Jf -Srv $srv -Method Get -Path '/Packages' -Token $token | ForEach-Object { $_ })
        $missing = @($Plugins | Where-Object { $n = $_; -not ($catalog | Where-Object { $_.name -eq $n }) })
        if ($missing.Count -gt 0) { Start-Sleep -Seconds 3 }
    } while ($missing.Count -gt 0 -and (Get-Date) -lt $deadline)
    $installed = @(Invoke-Jf -Srv $srv -Method Get -Path '/Plugins' -Token $token | ForEach-Object { $_ })
    $changed = $false
    foreach ($plugin in $Plugins) {
        $guid = $guids[$plugin]
        if (-not $guid) { throw "Unknown plugin '$plugin'" }
        if ($installed | Where-Object { ($_.Id -replace '-', '') -eq ($guid -replace '-', '') }) {
            Write-Host "    [$name] $plugin already installed"
            continue
        }

        Write-Host "==> [$name] installing $plugin"
        $path = '/Packages/Installed/' + [Uri]::EscapeDataString($plugin) + '?assemblyGuid=' + $guid + '&repositoryUrl=' + [Uri]::EscapeDataString($repoUrl)
        Invoke-Jf -Srv $srv -Method Post -Path $path -Token $token | Out-Null
        $changed = $true
    }

    if ($installed | Where-Object { $_.Status -eq 'Restart' }) { $changed = $true }
    if ($changed) {
        # Wait for the downloads to finish before restarting.
        $deadline = (Get-Date).AddSeconds(120)
        do {
            Start-Sleep -Seconds 3
            $now = @(Invoke-Jf -Srv $srv -Method Get -Path '/Plugins' -Token $token | ForEach-Object { $_.Id -replace '-', '' })
            $pending = @($Plugins | Where-Object { $now -notcontains ($guids[$_] -replace '-', '') })
        } while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline)

        Invoke-Native docker @('compose', '-f', $srv.Compose, 'restart')
        Wait-JfReady $srv | Out-Null
        Start-Sleep -Seconds 5
        $token = Get-AdminToken $srv
    }

    $loaded = @(Invoke-Jf -Srv $srv -Method Get -Path '/Plugins' -Token $token | ForEach-Object { $_ })
    foreach ($p in $loaded) { Write-Host ("    [{0}] {1} {2} {3}" -f $name, $p.Name, $p.Version, $p.Status) }
}
