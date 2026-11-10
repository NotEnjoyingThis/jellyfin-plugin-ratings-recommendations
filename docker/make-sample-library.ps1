<#
.SYNOPSIS
    Generates a tiny sample library (invented titles) for the Docker test servers.

.DESCRIPTION
    Creates docker/sample-library/movies and docker/sample-library/shows with 5-second
    test videos (ffmpeg) and Kodi-style .nfo files: genres, directors, writers, actors,
    guest stars, tags, studios, collections and community ratings. All names are fictional.
    Compatible with Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER Force
    Delete and regenerate the library if it already exists.
#>
[CmdletBinding()]
param([switch]$Force)

. "$PSScriptRoot\common.ps1"

$library = Join-Path $script:DockerDir 'sample-library'
if (Test-Path $library) {
    if (-not $Force) { Write-Host "Sample library already exists: $library (use -Force to regenerate)"; return }
    Remove-Item -Recurse -Force $library
}

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    throw 'ffmpeg not found. Install it with: winget install Gyan.FFmpeg'
}

$moviesDir = Join-Path $library 'movies'
$showsDir = Join-Path $library 'shows'
New-Item -ItemType Directory -Force -Path $moviesDir, $showsDir | Out-Null

# One 5-second clip, copied everywhere.
$clip = Join-Path $library 'clip.mp4'
$ffArgs = @('-hide_banner', '-loglevel', 'error', '-y',
    '-f', 'lavfi', '-i', 'testsrc=duration=5:size=320x240:rate=24',
    '-f', 'lavfi', '-i', 'sine=frequency=440:duration=5',
    '-c:v', 'libx264', '-preset', 'ultrafast', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-shortest', $clip)
& ffmpeg @ffArgs
if ($LASTEXITCODE -ne 0) { throw 'ffmpeg failed' }

function Escape-Xml([string]$s) { return [System.Security.SecurityElement]::Escape($s) }

function Write-Text([string]$path, [string]$content) {
    [System.IO.File]::WriteAllText($path, $content, $script:Utf8NoBom)
}

function Get-PeopleXml($directors, $writers, $actors) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($d in $directors) { [void]$sb.AppendLine('  <director>' + (Escape-Xml $d) + '</director>') }
    foreach ($w in $writers) { [void]$sb.AppendLine('  <credits>' + (Escape-Xml $w) + '</credits>') }
    $order = 0
    foreach ($a in $actors) {
        [void]$sb.AppendLine('  <actor><name>' + (Escape-Xml $a) + '</name><role>Role ' + ($order + 1) + '</role><order>' + $order + '</order></actor>')
        $order++
    }
    return $sb.ToString()
}

# title | year | genres | directors | writers | actors | tags | studio | collection | rating
$movies = @(
    @('Orbit of Glass', 1982, 'Science Fiction,Horror', 'Ilaria Venn', 'Marco Dell', 'Ada Ross,Luca Ferri,Nina Hale,Piet Sorel,Gemma Rae', 'space,alien', 'Northlight Pictures', 'Orbit Saga', 7.9),
    @('Orbit of Glass II', 1986, 'Science Fiction,Action', 'Ilaria Venn', 'Marco Dell', 'Ada Ross,Tom Vale,Nina Hale', 'space,sequel', 'Northlight Pictures', 'Orbit Saga', 7.1),
    @('Orbit of Glass: Origins', 2012, 'Science Fiction,Horror', 'Ilaria Venn', 'Sara Quill', 'Mira Lenz,Tom Vale,Oskar Brandt', 'space,prequel', 'Northlight Pictures', 'Orbit Saga', 6.4),
    @('Neon Rain', 1983, 'Science Fiction,Thriller', 'Ilaria Venn', 'Hugo Marr', 'Luca Ferri,Elsa Brook,Piet Sorel', 'cyberpunk,city', 'Northlight Pictures', '', 8.1),
    @('Sing Me Home', 2008, 'Musical,Comedy', 'Bruno Calla', 'Lia Penn', 'Gemma Rae,Rita Moss,Dino Vat', 'island,wedding', 'Sunny Days Studio', '', 6.2),
    @('Sing Me Home Again', 2018, 'Musical,Comedy', 'Bruno Calla', 'Lia Penn', 'Gemma Rae,Rita Moss', 'island,sequel', 'Sunny Days Studio', '', 5.8),
    @('The Quiet Harbour', 1994, 'Drama', 'Clara Juno', 'Clara Juno', 'Elsa Brook,Oskar Brandt,Ivo Kral', 'sea,family', 'Grey Pier Films', '', 7.6),
    @('Paper Lanterns', 2003, 'Drama,Romance', 'Clara Juno', 'Yuki Tam', 'Mira Lenz,Ivo Kral', 'love,festival', 'Grey Pier Films', '', 7.2),
    @('Fast Lane Heist', 2015, 'Action,Crime', 'Rex Daly', 'Rex Daly,Hugo Marr', 'Tom Vale,Dino Vat,Nina Hale', 'cars,robbery', 'Redline Pictures', '', 6.0),
    @('Laughing Gas', 1975, 'Comedy', 'Bruno Calla', 'Ugo Pratt', 'Rita Moss,Dino Vat', 'dentist,slapstick', 'Sunny Days Studio', '', 6.9),
    @('Under the Ice', 1999, 'Horror,Mystery', 'Vera Holm', 'Sara Quill', 'Ada Ross,Ivo Kral,Elsa Brook', 'arctic,monster', 'Coldframe', '', 7.0),
    @('The Last Archivist', 2021, 'Science Fiction,Drama', 'Vera Holm', 'Yuki Tam', 'Mira Lenz,Luca Ferri,Piet Sorel', 'library,future', 'Coldframe', '', 7.4)
)

foreach ($m in $movies) {
    $title = $m[0]; $year = $m[1]
    $folderName = ($title -replace '[:\\/*?"<>|]', '') + ' (' + $year + ')'
    $dir = Join-Path $moviesDir $folderName
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Copy-Item $clip (Join-Path $dir ($folderName + '.mp4'))

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8" standalone="yes"?>')
    [void]$sb.AppendLine('<movie>')
    [void]$sb.AppendLine('  <title>' + (Escape-Xml $title) + '</title>')
    [void]$sb.AppendLine('  <year>' + $year + '</year>')
    [void]$sb.AppendLine('  <premiered>' + $year + '-06-01</premiered>')
    [void]$sb.AppendLine('  <plot>Fictional test movie for the Ratings &amp; Recommendations plugin.</plot>')
    [void]$sb.AppendLine('  <rating>' + ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.0}', $m[9])) + '</rating>')
    foreach ($g in ($m[2] -split ',')) { [void]$sb.AppendLine('  <genre>' + (Escape-Xml $g) + '</genre>') }
    foreach ($t in ($m[6] -split ',')) { [void]$sb.AppendLine('  <tag>' + (Escape-Xml $t) + '</tag>') }
    [void]$sb.AppendLine('  <studio>' + (Escape-Xml $m[7]) + '</studio>')
    if ($m[8]) { [void]$sb.AppendLine('  <set><name>' + (Escape-Xml $m[8]) + '</name></set>') }
    [void]$sb.Append((Get-PeopleXml ($m[3] -split ',') ($m[4] -split ',') ($m[5] -split ',')))
    [void]$sb.AppendLine('</movie>')
    Write-Text (Join-Path $dir ($folderName + '.nfo')) $sb.ToString()
}

# Series: per-episode directors, writers and guest stars (to exercise season mode later).
$shows = @(
    @{ Title = 'Harbour Precinct'; Year = 2011; Genres = 'Crime,Drama'; Studio = 'Channel Nine North'; Cast = 'Oskar Brandt,Elsa Brook,Ivo Kral'; Tags = 'police,city'; Rating = 8.0;
       Seasons = @(
           @{ Number = 1; Year = 2011; Episodes = @(
               @{ Director = 'Clara Juno'; Writer = 'Hugo Marr'; Guests = 'Tom Vale,Rita Moss'; Rating = 7.8 },
               @{ Director = 'Rex Daly'; Writer = 'Hugo Marr'; Guests = 'Tom Vale'; Rating = 7.5 },
               @{ Director = 'Clara Juno'; Writer = 'Yuki Tam'; Guests = 'Dino Vat'; Rating = 8.1 }) },
           @{ Number = 2; Year = 2013; Episodes = @(
               @{ Director = 'Vera Holm'; Writer = 'Sara Quill'; Guests = 'Mira Lenz'; Rating = 8.4 },
               @{ Director = 'Vera Holm'; Writer = 'Sara Quill'; Guests = 'Mira Lenz,Ada Ross'; Rating = 8.6 },
               @{ Director = 'Rex Daly'; Writer = 'Sara Quill'; Guests = 'Ada Ross'; Rating = 8.2 }) }) },
    @{ Title = 'Tales from Nowhere'; Year = 2016; Genres = 'Science Fiction,Mystery'; Studio = 'Coldframe TV'; Cast = 'Mira Lenz,Luca Ferri'; Tags = 'anthology'; Rating = 7.3;
       Seasons = @(
           @{ Number = 1; Year = 2016; Episodes = @(
               @{ Director = ''; Writer = ''; Guests = ''; Rating = 7.0 },
               @{ Director = ''; Writer = ''; Guests = ''; Rating = 7.1 },
               @{ Director = ''; Writer = ''; Guests = ''; Rating = 6.9 }) },
           @{ Number = 2; Year = 2019; Episodes = @(
               @{ Director = 'Ilaria Venn'; Writer = 'Marco Dell'; Guests = 'Gemma Rae'; Rating = 7.9 },
               @{ Director = 'Ilaria Venn'; Writer = 'Marco Dell'; Guests = 'Piet Sorel'; Rating = 8.0 },
               @{ Director = 'Bruno Calla'; Writer = 'Lia Penn'; Guests = 'Gemma Rae'; Rating = 7.4 }) }) }
)

foreach ($s in $shows) {
    $showDir = Join-Path $showsDir $s.Title
    New-Item -ItemType Directory -Force -Path $showDir | Out-Null
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8" standalone="yes"?>')
    [void]$sb.AppendLine('<tvshow>')
    [void]$sb.AppendLine('  <title>' + (Escape-Xml $s.Title) + '</title>')
    [void]$sb.AppendLine('  <year>' + $s.Year + '</year>')
    [void]$sb.AppendLine('  <premiered>' + $s.Year + '-01-15</premiered>')
    [void]$sb.AppendLine('  <plot>Fictional test series for the Ratings &amp; Recommendations plugin.</plot>')
    [void]$sb.AppendLine('  <rating>' + ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.0}', $s.Rating)) + '</rating>')
    foreach ($g in ($s.Genres -split ',')) { [void]$sb.AppendLine('  <genre>' + (Escape-Xml $g) + '</genre>') }
    foreach ($t in ($s.Tags -split ',')) { [void]$sb.AppendLine('  <tag>' + (Escape-Xml $t) + '</tag>') }
    [void]$sb.AppendLine('  <studio>' + (Escape-Xml $s.Studio) + '</studio>')
    [void]$sb.Append((Get-PeopleXml @() @() ($s.Cast -split ',')))
    [void]$sb.AppendLine('</tvshow>')
    Write-Text (Join-Path $showDir 'tvshow.nfo') $sb.ToString()

    foreach ($season in $s.Seasons) {
        $seasonDir = Join-Path $showDir ('Season {0:00}' -f $season.Number)
        New-Item -ItemType Directory -Force -Path $seasonDir | Out-Null
        $ep = 1
        foreach ($e in $season.Episodes) {
            $base = '{0} S{1:00}E{2:00}' -f $s.Title, $season.Number, $ep
            Copy-Item $clip (Join-Path $seasonDir ($base + '.mp4'))
            $directors = @(); if ($e.Director) { $directors = $e.Director -split ',' }
            $writers = @(); if ($e.Writer) { $writers = $e.Writer -split ',' }
            $guests = @(); if ($e.Guests) { $guests = $e.Guests -split ',' }
            $eb = New-Object System.Text.StringBuilder
            [void]$eb.AppendLine('<?xml version="1.0" encoding="utf-8" standalone="yes"?>')
            [void]$eb.AppendLine('<episodedetails>')
            [void]$eb.AppendLine('  <title>Episode ' + $ep + '</title>')
            [void]$eb.AppendLine('  <season>' + $season.Number + '</season>')
            [void]$eb.AppendLine('  <episode>' + $ep + '</episode>')
            [void]$eb.AppendLine('  <aired>' + $season.Year + '-0' + $ep + '-10</aired>')
            [void]$eb.AppendLine('  <rating>' + ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.0}', $e.Rating)) + '</rating>')
            [void]$eb.Append((Get-PeopleXml $directors $writers $guests))
            [void]$eb.AppendLine('</episodedetails>')
            Write-Text (Join-Path $seasonDir ($base + '.nfo')) $eb.ToString()
            $ep++
        }
    }
}

Remove-Item $clip
Write-Host ("Sample library ready: {0} movies, {1} series in {2}" -f $movies.Count, $shows.Count, $library)
