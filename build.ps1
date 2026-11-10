<#
.SYNOPSIS
    Builds the two plugin zips (Jellyfin 10.11 and Jellyfin 12) with MD5 and SHA-256 files.

.DESCRIPTION
    Works in Windows PowerShell 5.1 and PowerShell 7+.
    Output: artifacts/ratings-recommendations_<version>_jf10.11.zip and _jf12.zip,
    each containing only Jellyfin.Plugin.RatingsRecs.dll and meta.json.

.PARAMETER Version
    Plugin version (4 numbers). Defaults to the version in build.yaml.

.PARAMETER Only
    Build only one target: "jf10.11" or "jf12".

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Version 1.2.3.0
    .\build.ps1 -Only jf12
#>
[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$Only = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$artifacts = Join-Path $root 'artifacts'
$project = Join-Path $root 'src/Jellyfin.Plugin.RatingsRecs/Jellyfin.Plugin.RatingsRecs.csproj'
$dllName = 'Jellyfin.Plugin.RatingsRecs.dll'

function Get-YamlScalar([string]$text, [string]$key) {
    $m = [regex]::Match($text, '(?m)^' + [regex]::Escape($key) + ':\s*"([^"]*)"')
    if (-not $m.Success) { throw "build.yaml: missing key '$key'" }
    return $m.Groups[1].Value
}

function Get-YamlFolded([string]$text, [string]$key) {
    $m = [regex]::Match($text, '(?ms)^' + [regex]::Escape($key) + ':\s*>\s*\r?\n((?:[ \t]+[^\r\n]*\r?\n?)+)')
    if (-not $m.Success) { return Get-YamlScalar $text $key }
    $lines = $m.Groups[1].Value -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    return ($lines -join ' ')
}

function ConvertTo-JsonString([string]$value) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $value.ToCharArray()) {
        $code = [int]$ch
        if ($ch -eq '"') { [void]$sb.Append('\"') }
        elseif ($ch -eq '\') { [void]$sb.Append('\\') }
        elseif ($ch -eq "`n") { [void]$sb.Append('\n') }
        elseif ($ch -eq "`r") { [void]$sb.Append('\r') }
        elseif ($ch -eq "`t") { [void]$sb.Append('\t') }
        elseif ($code -lt 32) { [void]$sb.Append(('\u{0:x4}' -f $code)) }
        else { [void]$sb.Append($ch) }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Get-ChangelogSection([string]$version) {
    $path = Join-Path $root 'CHANGELOG.md'
    if (-not (Test-Path $path)) { return '' }
    $semver = ($version -split '\.')[0..2] -join '.'
    $text = [System.IO.File]::ReadAllText($path, $utf8NoBom)
    $m = [regex]::Match($text, '(?ms)^## \[' + [regex]::Escape($semver) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
    if (-not $m.Success) { return '' }
    return $m.Groups[1].Value.Trim()
}

function Get-BuildTimestamp {
    if ($env:SOURCE_DATE_EPOCH) {
        return [DateTimeOffset]::FromUnixTimeSeconds([long]$env:SOURCE_DATE_EPOCH).UtcDateTime
    }
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git) {
        # Windows PowerShell 5.1 turns native stderr into errors under 'Stop'; relax it for this call.
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { $epoch = & git -C $root log -1 --format=%ct 2>$null } catch { $epoch = $null }
        $ErrorActionPreference = $previous
        if ($LASTEXITCODE -eq 0 -and $epoch) {
            return [DateTimeOffset]::FromUnixTimeSeconds([long]$epoch).UtcDateTime
        }
    }
    return New-Object DateTime(2026, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)
}

function Get-FileHashLower([string]$path, [string]$algorithm) {
    return (Get-FileHash -Path $path -Algorithm $algorithm).Hash.ToLowerInvariant()
}

$yaml = [System.IO.File]::ReadAllText((Join-Path $root 'build.yaml'), $utf8NoBom)
if (-not $Version) { $Version = Get-YamlScalar $yaml 'version' }
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Version must have 4 numbers (e.g. 1.0.0.0), got '$Version'" }

$guid = Get-YamlScalar $yaml 'guid'
$name = Get-YamlScalar $yaml 'name'
$owner = Get-YamlScalar $yaml 'owner'
$category = Get-YamlScalar $yaml 'category'
$overview = Get-YamlScalar $yaml 'overview'
$description = Get-YamlFolded $yaml 'description'
$changelog = Get-ChangelogSection $Version
if (-not $changelog) { $changelog = Get-YamlFolded $yaml 'changelog' }

$builds = @()
foreach ($m in [regex]::Matches($yaml, '(?m)^\s*-\s*id:\s*"([^"]+)"\s*\r?\n\s*framework:\s*"([^"]+)"\s*\r?\n\s*targetAbi:\s*"([^"]+)"')) {
    $builds += New-Object PSObject -Property @{ Id = $m.Groups[1].Value; Framework = $m.Groups[2].Value; TargetAbi = $m.Groups[3].Value }
}
if ($builds.Count -eq 0) { throw 'build.yaml: no builds found' }
if ($Only) { $builds = @($builds | Where-Object { $_.Id -eq $Only }) }
if ($builds.Count -eq 0) { throw "Unknown build '$Only'" }

$timestamp = Get-BuildTimestamp
$timestampText = $timestamp.ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture)

Add-Type -AssemblyName System.IO.Compression
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

foreach ($b in $builds) {
    $staging = Join-Path $artifacts ('staging/' + $b.Id)
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    Write-Host "==> Publishing $($b.Id) ($($b.Framework), targetAbi $($b.TargetAbi), version $Version)"
    # Full rebuild so that the reproducible-build properties always apply.
    & dotnet build $project -c Release -f $b.Framework --no-incremental "-p:Version=$Version" -p:ContinuousIntegrationBuild=true --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $($b.Id)" }
    & dotnet publish $project -c Release -f $b.Framework --no-build -o $staging "-p:Version=$Version" -p:ContinuousIntegrationBuild=true --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($b.Id)" }

    $dlls = @(Get-ChildItem -Path $staging -Filter '*.dll' | ForEach-Object { $_.Name })
    $unexpected = @($dlls | Where-Object { $_ -ne $dllName })
    if ($unexpected.Count -gt 0) { throw ("Unexpected DLLs in publish output: " + ($unexpected -join ', ')) }
    if ($dlls -notcontains $dllName) { throw "$dllName not found in publish output" }

    $meta = "{`n" +
        '    "category": ' + (ConvertTo-JsonString $category) + ",`n" +
        '    "changelog": ' + (ConvertTo-JsonString $changelog) + ",`n" +
        '    "description": ' + (ConvertTo-JsonString $description) + ",`n" +
        '    "guid": ' + (ConvertTo-JsonString $guid) + ",`n" +
        '    "name": ' + (ConvertTo-JsonString $name) + ",`n" +
        '    "overview": ' + (ConvertTo-JsonString $overview) + ",`n" +
        '    "owner": ' + (ConvertTo-JsonString $owner) + ",`n" +
        '    "targetAbi": ' + (ConvertTo-JsonString $b.TargetAbi) + ",`n" +
        '    "timestamp": ' + (ConvertTo-JsonString $timestampText) + ",`n" +
        '    "version": ' + (ConvertTo-JsonString $Version) + ",`n" +
        '    "assemblies": [ ' + (ConvertTo-JsonString $dllName) + " ]`n" +
        "}`n"
    $metaPath = Join-Path $staging 'meta.json'
    [System.IO.File]::WriteAllText($metaPath, $meta, $utf8NoBom)

    $zipName = 'ratings-recommendations_' + $Version + '_' + $b.Id + '.zip'
    $zipPath = Join-Path $artifacts $zipName
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

    # Deterministic zip: fixed entry order and timestamps.
    $fileStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive($fileStream, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($entryName in @($dllName, 'meta.json')) {
                $entry = $zip.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = New-Object DateTimeOffset($timestamp.Year, $timestamp.Month, $timestamp.Day, $timestamp.Hour, $timestamp.Minute, $timestamp.Second, [TimeSpan]::Zero)
                $bytes = [System.IO.File]::ReadAllBytes((Join-Path $staging $entryName))
                $stream = $entry.Open()
                try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $fileStream.Dispose() }

    $md5 = Get-FileHashLower $zipPath 'MD5'
    $sha256 = Get-FileHashLower $zipPath 'SHA256'
    [System.IO.File]::WriteAllText($zipPath + '.md5', ($md5 + '  ' + $zipName + "`n"), $utf8NoBom)
    [System.IO.File]::WriteAllText($zipPath + '.sha256', ($sha256 + '  ' + $zipName + "`n"), $utf8NoBom)

    Write-Host ("    {0}`n    md5    {1}`n    sha256 {2}" -f $zipPath, $md5, $sha256)
}
