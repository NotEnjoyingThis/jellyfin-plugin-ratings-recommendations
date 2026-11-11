# Shared helpers for the Docker test-server scripts. Dot-source it: . "$PSScriptRoot\common.ps1"
# Compatible with Windows PowerShell 5.1 and PowerShell 7+. Keep this file ASCII-only.

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:DockerDir = $PSScriptRoot
$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Read-TestEnv {
    $path = Join-Path $script:DockerDir '.env'
    if (-not (Test-Path $path)) { $path = Join-Path $script:DockerDir '.env.example' }
    $values = @{}
    foreach ($line in [System.IO.File]::ReadAllLines($path)) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
        $idx = $trimmed.IndexOf('=')
        if ($idx -gt 0) { $values[$trimmed.Substring(0, $idx).Trim()] = $trimmed.Substring($idx + 1).Trim() }
    }
    return $values
}

function Get-TestServer([string]$Server) {
    if ($Server -eq '10.11') {
        return New-Object PSObject -Property @{ Name = '10.11'; Port = 8811; Container = 'rr-jellyfin-10.11'; Compose = (Join-Path $script:DockerDir 'compose.10.11.yml'); DataDir = (Join-Path $script:DockerDir '.data/10.11'); BuildId = 'jf10.11' }
    }
    if ($Server -eq '12') {
        return New-Object PSObject -Property @{ Name = '12'; Port = 8812; Container = 'rr-jellyfin-12'; Compose = (Join-Path $script:DockerDir 'compose.12.yml'); DataDir = (Join-Path $script:DockerDir '.data/12'); BuildId = 'jf12' }
    }
    # Clean servers for release acceptance tests: the plugin comes from the published catalog.
    if ($Server -eq 'clean-10.11') {
        return New-Object PSObject -Property @{ Name = 'clean-10.11'; Port = 8821; Container = 'rr-jellyfin-clean-10.11'; Compose = (Join-Path $script:DockerDir 'compose.clean-10.11.yml'); DataDir = (Join-Path $script:DockerDir '.data/clean-10.11'); BuildId = 'jf10.11' }
    }
    if ($Server -eq 'clean-12') {
        return New-Object PSObject -Property @{ Name = 'clean-12'; Port = 8822; Container = 'rr-jellyfin-clean-12'; Compose = (Join-Path $script:DockerDir 'compose.clean-12.yml'); DataDir = (Join-Path $script:DockerDir '.data/clean-12'); BuildId = 'jf12' }
    }
    throw "Unknown server '$Server' (use 10.11, 12, clean-10.11 or clean-12)"
}

function Get-AuthHeader([string]$Token) {
    $value = 'MediaBrowser Client="rr-dev-scripts", Device="script", DeviceId="rr-dev-scripts", Version="1.0.0"'
    if ($Token) { $value += ', Token="' + $Token + '"' }
    return @{ Authorization = $value }
}

function Invoke-Jf {
    param(
        [Parameter(Mandatory = $true)] $Srv,
        [Parameter(Mandatory = $true)] [string]$Method,
        [Parameter(Mandatory = $true)] [string]$Path,
        $Body = $null,
        [string]$Token = ''
    )
    $uri = 'http://localhost:' + $Srv.Port + $Path
    $params = @{ Uri = $uri; Method = $Method; Headers = (Get-AuthHeader $Token); UseBasicParsing = $true }
    if ($null -ne $Body) {
        $params['ContentType'] = 'application/json; charset=utf-8'
        $params['Body'] = $script:Utf8NoBom.GetBytes((ConvertTo-Json -InputObject $Body -Depth 10 -Compress))
    }
    # Jellyfin answers 503 while it is still starting up: retry for a while.
    $attempt = 0
    while ($true) {
        try { return Invoke-RestMethod @params }
        catch {
            $status = 0
            if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
            $attempt++
            if ($status -ne 503 -or $attempt -ge 60) { throw }
            Start-Sleep -Seconds 2
        }
    }
}

function Invoke-Native {
    # Runs a native command; Windows PowerShell 5.1 would turn its stderr into terminating errors.
    param([Parameter(Mandatory = $true)][string]$Command, [string[]]$Arguments = @())
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Command @Arguments 2>&1 | ForEach-Object { Write-Host ('    ' + $_) } }
    finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "$Command $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Wait-JfReady($Srv, [int]$TimeoutSeconds = 180) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $info = Invoke-Jf -Srv $Srv -Method Get -Path '/System/Info/Public'
            if ($info) { return $info }
        }
        catch { Start-Sleep -Seconds 2 }
    }
    throw "Jellyfin $($Srv.Name) did not answer on port $($Srv.Port) within $TimeoutSeconds s"
}

function Get-AdminToken($Srv) {
    $envValues = Read-TestEnv
    $auth = Invoke-Jf -Srv $Srv -Method Post -Path '/Users/AuthenticateByName' -Body @{ Username = $envValues['JF_ADMIN_USER']; Pw = $envValues['JF_ADMIN_PASSWORD'] }
    return $auth.AccessToken
}
