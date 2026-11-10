<#
.SYNOPSIS
    Starts a Docker test server and prepares it: startup wizard, test users, sample libraries.

.DESCRIPTION
    Idempotent: steps already done are skipped. Credentials come from docker/.env
    (or docker/.env.example). Compatible with Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER Server
    "10.11" or "12".

.EXAMPLE
    .\docker\setup-server.ps1 -Server 10.11
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][ValidateSet('10.11', '12')][string]$Server)

. "$PSScriptRoot\common.ps1"

$srv = Get-TestServer $Server
$envValues = Read-TestEnv

foreach ($sub in @('config', 'cache')) { New-Item -ItemType Directory -Force -Path (Join-Path $srv.DataDir $sub) | Out-Null }
if (-not (Test-Path (Join-Path $script:DockerDir 'sample-library'))) { & (Join-Path $PSScriptRoot 'make-sample-library.ps1') }

Write-Host "==> Starting Jellyfin $($srv.Name) on http://localhost:$($srv.Port)"
Invoke-Native docker @('compose', '-f', $srv.Compose, 'up', '-d', '--quiet-pull')

$info = Wait-JfReady $srv
Write-Host "    Server version $($info.Version), wizard completed: $($info.StartupWizardCompleted)"

if (-not $info.StartupWizardCompleted) {
    Write-Host '==> Completing the startup wizard'
    Invoke-Jf -Srv $srv -Method Post -Path '/Startup/Configuration' -Body @{ UICulture = 'en-US'; MetadataCountryCode = 'US'; PreferredMetadataLanguage = 'en' } | Out-Null
    Invoke-Jf -Srv $srv -Method Get -Path '/Startup/User' | Out-Null
    Invoke-Jf -Srv $srv -Method Post -Path '/Startup/User' -Body @{ Name = $envValues['JF_ADMIN_USER']; Password = $envValues['JF_ADMIN_PASSWORD'] } | Out-Null
    Invoke-Jf -Srv $srv -Method Post -Path '/Startup/RemoteAccess' -Body @{ EnableRemoteAccess = $true; EnableAutomaticPortMapping = $false } | Out-Null
    Invoke-Jf -Srv $srv -Method Post -Path '/Startup/Complete' | Out-Null
}

$token = Get-AdminToken $srv

$users = @(Invoke-Jf -Srv $srv -Method Get -Path '/Users' -Token $token | ForEach-Object { $_ })
if (-not ($users | Where-Object { $_.Name -eq $envValues['JF_USER2_NAME'] })) {
    Write-Host "==> Creating test user $($envValues['JF_USER2_NAME'])"
    Invoke-Jf -Srv $srv -Method Post -Path '/Users/New' -Token $token -Body @{ Name = $envValues['JF_USER2_NAME']; Password = $envValues['JF_USER2_PASSWORD'] } | Out-Null
}

# Local metadata only: the sample titles are fictional, no internet fetchers needed.
function New-LibraryOptions([string]$path, [string[]]$types) {
    $typeOptions = @()
    foreach ($t in $types) { $typeOptions += @{ Type = $t; MetadataFetchers = @(); ImageFetchers = @() } }
    return @{
        LibraryOptions = @{
            EnableRealtimeMonitor = $false
            EnableChapterImageExtraction = $false
            ExtractChapterImagesDuringLibraryScan = $false
            AutomaticallyAddToCollection = $true
            SaveLocalMetadata = $false
            MetadataSavers = @()
            TypeOptions = $typeOptions
            PathInfos = @(@{ Path = $path })
        }
    }
}

$folders = @(Invoke-Jf -Srv $srv -Method Get -Path '/Library/VirtualFolders' -Token $token | ForEach-Object { $_ })
$libraries = @(
    @{ Name = 'Movies'; Type = 'movies'; Path = '/media/movies'; Types = @('Movie') },
    @{ Name = 'Shows'; Type = 'tvshows'; Path = '/media/shows'; Types = @('Series', 'Season', 'Episode') }
)
foreach ($lib in $libraries) {
    if ($folders | Where-Object { $_.Name -eq $lib.Name }) { continue }
    Write-Host "==> Adding library $($lib.Name)"
    $query = '/Library/VirtualFolders?name=' + [Uri]::EscapeDataString($lib.Name) + '&collectionType=' + $lib.Type + '&refreshLibrary=true'
    Invoke-Jf -Srv $srv -Method Post -Path $query -Token $token -Body (New-LibraryOptions $lib.Path $lib.Types) | Out-Null
}

Write-Host "Ready: http://localhost:$($srv.Port)  (credentials in docker/.env or docker/.env.example)"
