<#
.SYNOPSIS
    Installs plugins from their public catalogs on the Docker test servers, the way an administrator does
    (Dashboard > Plugins > Repositories + Catalog), then restarts the servers.

.DESCRIPTION
    Companion plugins come from the IAmParadox27 repository; "Ratings & Recommendations" comes from this project's
    published manifest (used on the clean servers for release acceptance tests).

.PARAMETER Server
    "10.11", "12", "all" (both development servers), "clean-10.11" or "clean-12".

.PARAMETER Plugins
    Plugin names to install. Default: File Transformation.

.EXAMPLE
    .\docker\install-deps.ps1 -Server all
    .\docker\install-deps.ps1 -Server all -Plugins 'File Transformation','Plugin Pages','Home Screen Sections'
    .\docker\install-deps.ps1 -Server clean-12 -Plugins 'File Transformation','Ratings & Recommendations'
#>
[CmdletBinding()]
param(
    [ValidateSet('10.11', '12', 'all', 'clean-10.11', 'clean-12')][string]$Server = 'all',
    [string[]]$Plugins = @('File Transformation')
)

. "$PSScriptRoot\common.ps1"
Set-StrictMode -Off

# 'powershell -File' passes a comma-separated list as one string: split it.
$Plugins = @($Plugins | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$paradox = @{ Name = 'IAmParadox27 plugins'; Url = 'https://www.iamparadox.dev/jellyfin/plugins/manifest.json' }
$ours = @{ Name = 'Ratings & Recommendations'; Url = 'https://raw.githubusercontent.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/main/manifest.json' }
$known = @{
    'File Transformation' = @{ Guid = '5e87cc92-571a-4d8d-8d98-d2d4147f9f90'; Repo = $paradox }
    'Home Screen Sections' = @{ Guid = 'b8298e01-2697-407a-b44d-aa8dc795e850'; Repo = $paradox }
    'Plugin Pages' = @{ Guid = '5b6550fa-a014-4f4c-8a2c-59a43680ac6d'; Repo = $paradox }
    'Ratings & Recommendations' = @{ Guid = '4d56ae7e-38cb-431c-bff8-e135c2087aa5'; Repo = $ours }
}
foreach ($plugin in $Plugins) { if (-not $known[$plugin]) { throw "Unknown plugin '$plugin'" } }
$guids = @{}
foreach ($plugin in $Plugins) { $guids[$plugin] = $known[$plugin].Guid }

$targets = @($Server)
if ($Server -eq 'all') { $targets = @('10.11', '12') }

foreach ($name in $targets) {
    $srv = Get-TestServer $name
    Wait-JfReady $srv | Out-Null
    $token = Get-AdminToken $srv

    $repos = @(Invoke-Jf -Srv $srv -Method Get -Path '/Repositories' -Token $token | ForEach-Object { $_ })
    $list = @($repos | ForEach-Object { @{ Name = $_.Name; Url = $_.Url; Enabled = $_.Enabled } })
    $added = $false
    foreach ($repo in @($Plugins | ForEach-Object { $known[$_].Repo } | Sort-Object { $_.Url } -Unique)) {
        if (-not ($repos | Where-Object { $_.Url -eq $repo.Url })) {
            Write-Host "==> [$name] adding repository $($repo.Url)"
            $list += @{ Name = $repo.Name; Url = $repo.Url; Enabled = $true }
            $added = $true
        }
    }

    if ($added) {
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
        $path = '/Packages/Installed/' + [Uri]::EscapeDataString($plugin) + '?assemblyGuid=' + $guid + '&repositoryUrl=' + [Uri]::EscapeDataString($known[$plugin].Repo.Url)
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
