<#
.SYNOPSIS
    Shows the plugin status on the Docker test servers (version, status, config page, log errors).

.EXAMPLE
    .\docker\status.ps1
#>
[CmdletBinding()]
param([ValidateSet('10.11', '12', 'all', 'clean-10.11', 'clean-12')][string]$Server = 'all')

. "$PSScriptRoot\common.ps1"

$pluginGuid = '4d56ae7e38cb431cbff8e135c2087aa5'
$targets = @($Server)
if ($Server -eq 'all') { $targets = @('10.11', '12') }
$failed = $false

foreach ($name in $targets) {
    $srv = Get-TestServer $name
    try {
        $info = Wait-JfReady $srv 60
        $token = Get-AdminToken $srv
        $plugins = @(Invoke-Jf -Srv $srv -Method Get -Path '/Plugins' -Token $token | ForEach-Object { $_ })
        $self = $plugins | Where-Object { ($_.Id -replace '-', '') -eq $pluginGuid }
        $pageStatus = 'missing'
        try {
            $page = Invoke-WebRequest -UseBasicParsing -Headers (Get-AuthHeader $token) -Uri ('http://localhost:' + $srv.Port + '/web/ConfigurationPage?name=' + [Uri]::EscapeDataString('Ratings & Recommendations'))
            if ($page.Content -match 'RatingsRecsConfigPage') { $pageStatus = 'OK (' + $page.StatusCode + ')' }
        }
        catch { $pageStatus = 'error: ' + $_.Exception.Message }

        if ($self) {
            Write-Host ("Jellyfin {0,-7} server {1,-8} plugin {2} status {3,-8} config page {4}" -f $name, $info.Version, $self.Version, $self.Status, $pageStatus)
            if ($self.Status -ne 'Active' -or -not $pageStatus.StartsWith('OK')) { $failed = $true }
        }
        else {
            Write-Host ("Jellyfin {0,-7} server {1,-8} plugin NOT LOADED" -f $name, $info.Version)
            $failed = $true
        }

        $logs = docker logs $srv.Container 2>&1 | Select-String -Pattern 'RatingsRecs' | Select-String -Pattern '\[ERR\]|\[WRN\]|Exception'
        foreach ($line in $logs) { Write-Host ('    log: ' + $line) }
    }
    catch {
        Write-Host ("Jellyfin {0,-7} unreachable: {1}" -f $name, $_.Exception.Message)
        $failed = $true
    }
}

if ($failed) { exit 1 }
