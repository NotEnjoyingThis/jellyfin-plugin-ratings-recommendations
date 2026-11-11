<#
.SYNOPSIS
    End-to-end checks of the ratings API on the Docker test servers (milestone M1 acceptance).

.DESCRIPTION
    Uses the test accounts of docker/.env(.example). Covers: PUT -> GET, native Likes/Rating/IsFavorite
    mirror (design section 4), removal, user isolation, admin access, Favourite from another client
    (implicit +3), CSV export/import, Favourites import, validation errors and persistence after restart.
    Compatible with Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER Server
    "10.11", "12" or "all".

.PARAMETER SkipRestart
    Skip the restart/persistence check (faster).
#>
[CmdletBinding()]
param(
    [ValidateSet('10.11', '12', 'all', 'clean-10.11', 'clean-12')][string]$Server = 'all',
    [switch]$SkipRestart
)

. "$PSScriptRoot\common.ps1"

# Jellyfin omits null properties from JSON: missing properties must read as $null here.
Set-StrictMode -Off
$script:Failures = 0

function Check([string]$name, [bool]$condition, [string]$detail = '') {
    if ($condition) { Write-Host "  PASS  $name" }
    else { Write-Host "  FAIL  $name $detail" -ForegroundColor Red; $script:Failures++ }
}

function Get-Status([scriptblock]$call) {
    try { & $call | Out-Null; return 200 }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return -1
    }
}

function Login($srv, [string]$user, [string]$password) {
    # Right after a restart Jellyfin may refuse logins (400) for a few seconds: retry.
    for ($attempt = 1; ; $attempt++) {
        try {
            $auth = Invoke-Jf -Srv $srv -Method Post -Path '/Users/AuthenticateByName' -Body @{ Username = $user; Pw = $password }
            return New-Object PSObject -Property @{ Token = $auth.AccessToken; Id = $auth.User.Id }
        }
        catch {
            if ($attempt -ge 30) { throw }
            Start-Sleep -Seconds 2
        }
    }
}

function Find-Item($srv, $session, [string]$type, [string]$name) {
    $path = '/Items?Recursive=true&IncludeItemTypes=' + $type + '&SearchTerm=' + [Uri]::EscapeDataString($name) + '&userId=' + $session.Id
    $result = Invoke-Jf -Srv $srv -Method Get -Path $path -Token $session.Token
    $match = @($result.Items | Where-Object { $_.Name -eq $name })
    if ($match.Count -eq 0) { throw "Item '$name' ($type) not found: is the library scanned?" }
    return $match[0].Id
}

function Get-Native($srv, $session, [string]$itemId) {
    return Invoke-Jf -Srv $srv -Method Get -Path ('/UserItems/' + $itemId + '/UserData?userId=' + $session.Id) -Token $session.Token
}

$envValues = Read-TestEnv
$targets = @($Server)
if ($Server -eq 'all') { $targets = @('10.11', '12') }

foreach ($name in $targets) {
    $srv = Get-TestServer $name
    Write-Host "== Jellyfin $name (http://localhost:$($srv.Port))"
    Wait-JfReady $srv | Out-Null
    $admin = Login $srv $envValues['JF_ADMIN_USER'] $envValues['JF_ADMIN_PASSWORD']
    $family = Login $srv $envValues['JF_USER2_NAME'] $envValues['JF_USER2_PASSWORD']

    # Start from the server default series mode for both test users (earlier manual tests may have changed it).
    foreach ($session in @($admin, $family)) {
        Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $session.Token -Body @{ seriesMode = $null } | Out-Null
    }

    $movie = Find-Item $srv $admin 'Movie' 'Neon Rain'
    $movie2 = Find-Item $srv $admin 'Movie' 'Paper Lanterns'
    $series = Find-Item $srv $admin 'Series' 'Harbour Precinct'
    $episodes = Invoke-Jf -Srv $srv -Method Get -Path ('/Items?Recursive=true&IncludeItemTypes=Episode&Limit=1&userId=' + $admin.Id) -Token $admin.Token
    $episode = $episodes.Items[0].Id

    # Clean slate for the items used here.
    foreach ($id in @($movie, $movie2, $series)) {
        Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $admin.Token | Out-Null
        Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $family.Token | Out-Null
        Invoke-Jf -Srv $srv -Method Delete -Path ('/UserFavoriteItems/' + $id + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    }

    # PUT -> GET and native mirror (design section 4).
    $table = @(
        @{ Value = -3; Rating = 2; Likes = $false; Favorite = $false },
        @{ Value = 3; Rating = 10; Likes = $true; Favorite = $true },
        @{ Value = 1; Rating = 7; Likes = $true; Favorite = $true },
        @{ Value = -1; Rating = 4; Likes = $false; Favorite = $false }
    )
    foreach ($row in $table) {
        $put = Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token -Body @{ value = $row.Value }
        $get = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token
        Check "PUT $($row.Value) -> GET" ($put.value -eq $row.Value -and $get.value -eq $row.Value -and $get.source -eq 'explicit')
        $native = Get-Native $srv $admin $movie
        Check "native mirror $($row.Value): Rating=$($row.Rating) Likes=$($row.Likes) IsFavorite=$($row.Favorite)" ($native.Rating -eq $row.Rating -and $native.Likes -eq $row.Likes -and $native.IsFavorite -eq $row.Favorite) ("got Rating=$($native.Rating) Likes=$($native.Likes) IsFavorite=$($native.IsFavorite)")
    }

    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token | Out-Null
    $get = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token
    $native = Get-Native $srv $admin $movie
    Check 'DELETE -> no rating, native Rating/Likes cleared' ($null -eq $get.value -and $null -eq $native.Rating -and $null -eq $native.Likes)

    # Bulk read.
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token -Body @{ value = 1 } | Out-Null
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $series) -Token $admin.Token -Body @{ value = 3 } | Out-Null
    $bulk = @(Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings?itemIds=' + $movie + ',' + $series + ',' + $movie2) -Token $admin.Token | ForEach-Object { $_ })
    Check 'bulk GET returns only rated items' ($bulk.Count -eq 2)

    # Isolation and admin access.
    $own = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie) -Token $family.Token
    Check 'other user does not see my rating' ($null -eq $own.value)
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie + '?userId=' + $admin.Id) -Token $family.Token }
    Check 'non-admin cannot read another user (403)' ($status -eq 403) "got $status"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Export?userId=' + $admin.Id) -Token $family.Token }
    Check 'non-admin cannot export another user (403)' ($status -eq 403) "got $status"
    $asAdmin = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie + '?userId=' + $family.Id) -Token $admin.Token
    Check 'admin can read another user' ($null -eq $asAdmin.value)

    # Validation.
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token -Body @{ value = 2 } }
    Check 'invalid value -> 400' ($status -eq 400) "got $status"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $episode) -Token $admin.Token -Body @{ value = 1 } }
    Check 'episode -> 400' ($status -eq 400) "got $status"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + [Guid]::NewGuid()) -Token $admin.Token -Body @{ value = 1 } }
    Check 'unknown item -> 404' ($status -eq 404) "got $status"
    $status = Get-Status { Invoke-RestMethod -UseBasicParsing -Uri ('http://localhost:' + $srv.Port + '/RatingsRecs/Ratings/' + $movie) }
    Check 'no authentication -> 401' ($status -eq 401) "got $status"

    # Favourite from another client (no thumbs) -> implicit +3; unfavourite removes it.
    Invoke-Jf -Srv $srv -Method Post -Path ('/UserFavoriteItems/' + $movie2 + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    $implicit = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token
    Check 'Favourite from other client -> implicit +3' ($implicit.value -eq 3 -and $implicit.source -eq 'implicit')
    Invoke-Jf -Srv $srv -Method Delete -Path ('/UserFavoriteItems/' + $movie2 + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    $implicit = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token
    Check 'Unfavourite -> implicit rating removed' ($null -eq $implicit.value)

    # Own writes do not loop back: a -3 removes the Favourite but must stay explicit -3.
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token -Body @{ value = 3 } | Out-Null
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token -Body @{ value = -3 } | Out-Null
    $afterLoop = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token
    Check 'own native writes are ignored by the listener' ($afterLoop.value -eq -3 -and $afterLoop.source -eq 'explicit')
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token | Out-Null

    # Favourites import.
    Invoke-Jf -Srv $srv -Method Post -Path ('/UserFavoriteItems/' + $series + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    $import = Invoke-Jf -Srv $srv -Method Post -Path '/RatingsRecs/ImportFavorites' -Token $family.Token
    $fromFav = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $series) -Token $family.Token
    Check 'ImportFavorites -> explicit +3' ($import.imported -ge 1 -and $fromFav.value -eq 3 -and $fromFav.source -eq 'explicit')

    # CSV export -> delete -> import.
    $csvResponse = Invoke-WebRequest -UseBasicParsing -Uri ('http://localhost:' + $srv.Port + '/RatingsRecs/Export') -Headers (Get-AuthHeader $admin.Token)
    $csv = [System.Text.Encoding]::UTF8.GetString($csvResponse.RawContentStream.ToArray())
    $movieDashed = ([Guid]$movie).ToString('D')
    Check 'Export CSV contains the rated items' ($csv.StartsWith('itemId,itemType,value') -and $csv.Contains($movieDashed) -and $csv.Contains('Neon Rain'))
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token | Out-Null
    $importCsv = Invoke-RestMethod -UseBasicParsing -Method Post -Uri ('http://localhost:' + $srv.Port + '/RatingsRecs/Import') -Headers (Get-AuthHeader $admin.Token) -ContentType 'text/csv' -Body ([System.Text.Encoding]::UTF8.GetBytes($csv))
    $restored = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie) -Token $admin.Token
    Check 'Import CSV restores the rating' ($restored.value -eq 1 -and $importCsv.added -ge 1) ("added=$($importCsv.added) value=$($restored.value)")
    $byName = "itemType,value,name,year`r`nMovie,-1,Paper Lanterns,2003`r`n"
    $importName = Invoke-RestMethod -UseBasicParsing -Method Post -Uri ('http://localhost:' + $srv.Port + '/RatingsRecs/Import') -Headers (Get-AuthHeader $family.Token) -ContentType 'text/csv' -Body ([System.Text.Encoding]::UTF8.GetBytes($byName))
    $matched = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $movie2) -Token $family.Token
    Check 'Import CSV matches by name and year' ($matched.value -eq -1) ("added=$($importName.added) unmatched=$($importName.unmatched)")

    # Recommendations (served from the background cache).
    $deadline = (Get-Date).AddSeconds(90)
    do {
        $recs = Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Recommendations?limit=200' -Token $family.Token
        if ($recs.Status -ne 'Ready') { Start-Sleep -Seconds 3 }
    } while ($recs.Status -ne 'Ready' -and (Get-Date) -lt $deadline)
    Check 'recommendations are computed in the background (Ready)' ($recs.Status -eq 'Ready') "status $($recs.Status)"
    $ids = @($recs.Items | ForEach-Object { $_.Id })
    Check 'recommendations have one explanation per item' ($ids.Count -gt 0 -and @($recs.Explanations).Count -eq $ids.Count) "items=$($ids.Count) total=$($recs.TotalRecordCount)"
    if ($ids.Count -eq 0) { continue }
    $ratedOnes = @(Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings?itemIds=' + ($ids -join ',')) -Token $family.Token | ForEach-Object { $_ })
    Check 'no rated item among the recommendations' ($ratedOnes.Count -eq 0) "found $($ratedOnes.Count)"
    $target = $ids[0]
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $target) -Token $family.Token -Body @{ value = -3 } | Out-Null
    $after = Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Recommendations?limit=200' -Token $family.Token
    Check 'an item rated -3 disappears at once' (-not (@($after.Items | ForEach-Object { $_.Id }) -contains $target))
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $target) -Token $family.Token | Out-Null
    $seriesRecs = Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Recommendations?type=Series&limit=50' -Token $family.Token
    Check 'type=Series returns only series' (@($seriesRecs.Items | Where-Object { $_.Type -ne 'Series' }).Count -eq 0)
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Recommendations?userId=' + $admin.Id) -Token $family.Token }
    Check 'non-admin cannot read recommendations of another user (403)' ($status -eq 403) "got $status"
    # "Rate these": watched but not rated, most recently watched first (NOTES deviation 43).
    $older = Find-Item $srv $family 'Movie' 'Under the Ice'
    $newer = Find-Item $srv $family 'Movie' 'Sing Me Home'
    $unwatched = Find-Item $srv $family 'Movie' 'Sing Me Home Again'
    $before = @{}
    foreach ($id in @($older, $newer, $unwatched)) {
        $before[$id] = (Get-Native $srv $family $id).Played
        Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $family.Token | Out-Null
    }
    Invoke-Jf -Srv $srv -Method Delete -Path ('/UserPlayedItems/' + $unwatched + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $older + '?userId=' + $family.Id + '&datePlayed=2026-01-02T20:00:00Z') -Token $family.Token | Out-Null
    Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $newer + '?userId=' + $family.Id + '&datePlayed=2026-01-05T20:00:00Z') -Token $family.Token | Out-Null
    # Marking as watched schedules a recomputation (30 s debounce): wait for it.
    $rateIds = @()
    for ($wait = 0; $wait -lt 45; $wait++) {
        $rateIds = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese?limit=100' -Token $family.Token).Items | ForEach-Object { $_.Id })
        if (($rateIds -contains $older) -and ($rateIds -contains $newer)) { break }
        Start-Sleep -Seconds 2
    }
    Check 'Rate these: watched and not rated items appear (recomputed after "watched")' (($rateIds -contains $older) -and ($rateIds -contains $newer)) "items=$($rateIds.Count)"
    Check 'Rate these: most recently watched first' ([array]::IndexOf($rateIds, $newer) -ge 0 -and [array]::IndexOf($rateIds, $newer) -lt [array]::IndexOf($rateIds, $older))
    Check 'Rate these: an unwatched item never appears' (-not ($rateIds -contains $unwatched))
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $newer) -Token $family.Token -Body @{ value = 1 } | Out-Null
    $afterRating = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese?limit=100' -Token $family.Token).Items | ForEach-Object { $_.Id })
    Check 'Rate these: a rated item leaves the list at once' ((-not ($afterRating -contains $newer)) -and ($afterRating -contains $older))
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Onboarding' -Token $family.Token }
    Check 'the old /Onboarding endpoint is gone (404)' ($status -eq 404) "got $status"
    foreach ($id in @($older, $newer, $unwatched)) {
        Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $family.Token | Out-Null
        if (-not $before[$id]) { Invoke-Jf -Srv $srv -Method Delete -Path ('/UserPlayedItems/' + $id + '?userId=' + $family.Id) -Token $family.Token | Out-Null }
    }

    # "Watched, to rate" with the series rating modes, un-watching, permissions, implicit ratings, Home Screen Sections.
    $rateIdsOf = { @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese?limit=200' -Token $family.Token).Items | ForEach-Object { $_.Id }) }
    $waitFor = {
        param([scriptblock]$condition)
        for ($attempt = 0; $attempt -lt 45; $attempt++) {
            if (& $condition) { return $true }
            Start-Sleep -Seconds 2
        }
        return $false
    }
    $seasonsOf = @(Invoke-Jf -Srv $srv -Method Get -Path ('/Items?Recursive=true&IncludeItemTypes=Season&ParentId=' + $series + '&userId=' + $family.Id) -Token $family.Token | ForEach-Object { $_.Items } | Sort-Object IndexNumber)
    $firstSeason = $seasonsOf[0].Id
    $seasonEpisodes = @(Invoke-Jf -Srv $srv -Method Get -Path ('/Shows/' + $series + '/Episodes?seasonId=' + $firstSeason + '&userId=' + $family.Id) -Token $family.Token | ForEach-Object { $_.Items } | Sort-Object IndexNumber)
    $episodeWasPlayed = @{}
    foreach ($e in $seasonEpisodes) { $episodeWasPlayed[$e.Id] = [bool]$e.UserData.Played }
    foreach ($id in @($series, $firstSeason)) { Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $family.Token | Out-Null }
    foreach ($e in $seasonEpisodes) { Invoke-Jf -Srv $srv -Method Delete -Path ('/UserPlayedItems/' + $e.Id + '?userId=' + $family.Id) -Token $family.Token | Out-Null }
    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = $null } | Out-Null

    Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $seasonEpisodes[0].Id + '?userId=' + $family.Id + '&datePlayed=2026-02-01T20:00:00Z') -Token $family.Token | Out-Null
    $ok = & $waitFor { (& $rateIdsOf) -contains $series }
    $ids = & $rateIdsOf
    Check 'whole series: a series with one episode watched is offered, not its season' ($ok -and -not ($ids -contains $firstSeason))

    foreach ($e in $seasonEpisodes) { Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $e.Id + '?userId=' + $family.Id + '&datePlayed=2026-02-02T20:00:00Z') -Token $family.Token | Out-Null }
    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = 'Season' } | Out-Null
    Check 'per season: the series disappears at once after the mode change' (-not ((& $rateIdsOf) -contains $series))
    $ok = & $waitFor { (& $rateIdsOf) -contains $firstSeason }
    Check 'per season: a fully watched season is offered (after the recomputation)' $ok
    $partial = @($seasonsOf | Select-Object -Skip 1 | ForEach-Object { $_.Id })
    Check 'per season: a season not watched is not offered' (@((& $rateIdsOf) | Where-Object { $partial -contains $_ }).Count -eq 0)

    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = 'Both' } | Out-Null
    $ok = & $waitFor { $now = & $rateIdsOf; ($now -contains $series) -and ($now -contains $firstSeason) }
    Check 'both: the series and the watched season are offered' $ok

    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $firstSeason) -Token $family.Token -Body @{ value = 3 } | Out-Null
    $now = & $rateIdsOf
    Check 'both: rating the season removes only the season' (($now -contains $series) -and -not ($now -contains $firstSeason))
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $firstSeason) -Token $family.Token | Out-Null

    foreach ($e in $seasonEpisodes) { Invoke-Jf -Srv $srv -Method Delete -Path ('/UserPlayedItems/' + $e.Id + '?userId=' + $family.Id) -Token $family.Token | Out-Null }
    $ok = & $waitFor { $now = & $rateIdsOf; -not ($now -contains $series) -and -not ($now -contains $firstSeason) }
    Check 'marking as not watched removes the series and the season (after the recomputation)' $ok
    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = $null } | Out-Null
    foreach ($e in $seasonEpisodes) {
        if ($episodeWasPlayed[$e.Id]) { Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $e.Id + '?userId=' + $family.Id) -Token $family.Token | Out-Null }
    }

    $movieOnly = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese?limit=200&type=Movie' -Token $family.Token).Items | Where-Object { $_.Type -ne 'Movie' })
    $tvOnly = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese?limit=200&type=Series' -Token $family.Token).Items | Where-Object { $_.Type -notin @('Series', 'Season') })
    Check '"watched, to rate" type=Movie / type=Series filter the kinds' (($movieOnly.Count -eq 0) -and ($tvOnly.Count -eq 0))
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese?type=Episode' -Token $family.Token }
    Check '"watched, to rate" with an invalid type -> 400' ($status -eq 400) "got $status"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/RateThese?userId=' + $admin.Id) -Token $family.Token }
    Check 'non-admin cannot read "watched, to rate" of another user (403)' ($status -eq 403) "got $status"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/RateThese?userId=' + $family.Id) -Token $admin.Token }
    Check 'admin can read "watched, to rate" of another user' ($status -eq 200) "got $status"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/RateThese' }
    Check '"watched, to rate" needs authentication (401)' ($status -eq 401) "got $status"

    $hearted = Find-Item $srv $family 'Movie' 'Under the Ice'
    $heartedWasPlayed = (Get-Native $srv $family $hearted).Played
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $hearted) -Token $family.Token | Out-Null
    Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $hearted + '?userId=' + $family.Id + '&datePlayed=2026-02-03T20:00:00Z') -Token $family.Token | Out-Null
    $ok = & $waitFor { (& $rateIdsOf) -contains $hearted }
    Invoke-Jf -Srv $srv -Method Post -Path ('/UserFavoriteItems/' + $hearted + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    $implicitRating = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $hearted) -Token $family.Token
    Check 'a heart from another app (implicit +3) removes the item from "watched, to rate"' ($ok -and $implicitRating.source -eq 'implicit' -and -not ((& $rateIdsOf) -contains $hearted)) "listed=$ok source=$($implicitRating.source)"
    Invoke-Jf -Srv $srv -Method Delete -Path ('/UserFavoriteItems/' + $hearted + '?userId=' + $family.Id) -Token $family.Token | Out-Null
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $hearted) -Token $family.Token | Out-Null
    if (-not $heartedWasPlayed) { Invoke-Jf -Srv $srv -Method Delete -Path ('/UserPlayedItems/' + $hearted + '?userId=' + $family.Id) -Token $family.Token | Out-Null }

    $hssHook = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Status' -Token $admin.Token).hooks | ForEach-Object { $_ } | Where-Object { $_.name -eq 'HomeScreenSections' })[0]
    if ($hssHook -and $hssHook.state -eq 'Active') {
        $hssIds = @((Invoke-Jf -Srv $srv -Method Get -Path ('/HomeScreen/Section/RatingsRecsRateThese?UserId=' + $family.Id) -Token $family.Token).Items | ForEach-Object { $_.Id })
        $apiIds = @(& $rateIdsOf | Select-Object -First $hssIds.Count)
        Check 'Home Screen Sections section = the same "watched, to rate" items' (($hssIds -join ',') -eq ($apiIds -join ',')) "hss=$($hssIds.Count) api=$($apiIds.Count)"
    }

    $times = @()
    for ($i = 0; $i -lt 7; $i++) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Recommendations?limit=20' -Token $family.Token | Out-Null
        $sw.Stop()
        $times += $sw.ElapsedMilliseconds
    }
    $median = ($times | Sort-Object)[3]
    Check "recommendations answer from cache in < 200 ms (median $median ms)" ($median -lt 200)

    # "Similar, picked for you" (design section 6.6, point 3).
    $similar = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Similar/' + $movie + '?limit=12') -Token $family.Token
    $simIds = @($similar.Items | ForEach-Object { $_.Id })
    Check 'Similar returns items with explanations, never the open item' ($simIds.Count -gt 0 -and -not ($simIds -contains $movie) -and @($similar.Explanations).Count -eq $simIds.Count) "items=$($simIds.Count)"
    $victim = $simIds[0]
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $victim) -Token $family.Token -Body @{ value = -1 } | Out-Null
    $similar = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Similar/' + $movie + '?limit=12') -Token $family.Token
    Check 'Similar never shows a negatively rated item' (-not (@($similar.Items | ForEach-Object { $_.Id }) -contains $victim))
    Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $victim) -Token $family.Token | Out-Null
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Similar/' + [Guid]::NewGuid()) -Token $family.Token }
    Check 'Similar on an unknown item -> 404' ($status -eq 404) "got $status"

    # Jellyfin 12 native "similar items" provider, per library (design M5).
    if ($srv.BuildId -eq 'jf12') {
        $pluginPath = '/Plugins/4d56ae7e38cb431cbff8e135c2087aa5/Configuration'
        $config = Invoke-Jf -Srv $srv -Method Get -Path $pluginPath -Token $admin.Token
        $savedIds = @($config.SimilarProviderLibraryIds)
        # PowerShell 5.1 passes a JSON array down the pipeline as one object: unroll it first.
        $folders = @(Invoke-Jf -Srv $srv -Method Get -Path '/Library/VirtualFolders' -Token $admin.Token | ForEach-Object { $_ })
        $moviesLibrary = @($folders | Where-Object { $_.CollectionType -eq 'movies' })[0]
        $nativePath = '/Items/' + $movie + '/Similar?limit=5&userId=' + $family.Id

        $config.SimilarProviderLibraryIds = @()
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
        $standard = @((Invoke-Jf -Srv $srv -Method Get -Path $nativePath -Token $family.Token).Items | ForEach-Object { $_.Id })
        $hated = $standard[0]
        Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $hated) -Token $family.Token -Body @{ value = -3 } | Out-Null

        $config.SimilarProviderLibraryIds = @($moviesLibrary.ItemId)
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
        $folder = @(Invoke-Jf -Srv $srv -Method Get -Path '/Library/VirtualFolders' -Token $admin.Token | ForEach-Object { $_ } | Where-Object { $_.ItemId -eq $moviesLibrary.ItemId })[0]
        $order = @(@($folder.LibraryOptions.TypeOptions | Where-Object { $_.Type -eq 'Movie' })[0].SimilarItemProviderOrder)
        Check 'provider on: placed first in the library order' ($order.Count -gt 0 -and $order[0] -eq 'Ratings & Recommendations') "order: $($order -join '|')"
        $providerHook = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Status' -Token $admin.Token).hooks | Where-Object { $_.name -eq 'SimilarProvider' })[0]
        Check 'provider hook is Active' ($providerHook.state -eq 'Active') "$($providerHook.state) $($providerHook.message)"
        $ranked = @((Invoke-Jf -Srv $srv -Method Get -Path $nativePath -Token $family.Token).Items)
        Check 'provider on: native Similar has no item rated -3' ($ranked.Count -eq 5 -and -not (@($ranked | ForEach-Object { $_.Id }) -contains $hated)) "items=$($ranked.Count)"
        Check 'provider on: native Similar keeps the item type' (@($ranked | Where-Object { $_.Type -ne 'Movie' }).Count -eq 0)
        $all = @((Invoke-Jf -Srv $srv -Method Get -Path ('/Items/' + $movie + '/Similar?limit=50&userId=' + $family.Id) -Token $family.Token).Items | ForEach-Object { $_.Id })
        Check 'provider on, small library: negatively rated items only at the end' ($all.Count -gt 0 -and $all[$all.Count - 1] -eq $hated) "last=$($all[$all.Count - 1])"

        $config.SimilarProviderLibraryIds = @()
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
        $folder = @(Invoke-Jf -Srv $srv -Method Get -Path '/Library/VirtualFolders' -Token $admin.Token | ForEach-Object { $_ } | Where-Object { $_.ItemId -eq $moviesLibrary.ItemId })[0]
        $order = @(@($folder.LibraryOptions.TypeOptions | Where-Object { $_.Type -eq 'Movie' })[0].SimilarItemProviderOrder)
        Check 'provider off: removed from the library order' (-not ($order -contains 'Ratings & Recommendations')) "order: $($order -join '|')"
        $back = @((Invoke-Jf -Srv $srv -Method Get -Path $nativePath -Token $family.Token).Items | ForEach-Object { $_.Id })
        Check 'provider off: native Similar is the standard one again' (($back -join ',') -eq ($standard -join ','))

        Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $hated) -Token $family.Token | Out-Null
        $config.SimilarProviderLibraryIds = $savedIds
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
    }

    # Jellyfin 10.11 optional override of /Items/Suggestions and /Movies/Recommendations (design M6).
    if ($srv.BuildId -eq 'jf10.11') {
        $pluginPath = '/Plugins/4d56ae7e38cb431cbff8e135c2087aa5/Configuration'
        $config = Invoke-Jf -Srv $srv -Method Get -Path $pluginPath -Token $admin.Token
        $savedOverride = [bool]$config.OverrideSuggestions
        $hookOf = { @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Status' -Token $admin.Token).hooks | ForEach-Object { $_ } | Where-Object { $_.name -eq 'SuggestionsOverride' })[0] }
        $suggestPath = '/Items/Suggestions?type=Movie&limit=5&userId=' + $family.Id

        $config.OverrideSuggestions = $false
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
        Check 'override off by default/after save: hook Disabled' ((& $hookOf).state -eq 'Disabled')

        $wasPlayed = (Get-Native $srv $family $movie).Played
        Invoke-Jf -Srv $srv -Method Post -Path ('/UserPlayedItems/' + $movie + '?userId=' + $family.Id) -Token $family.Token | Out-Null
        $hatedRec = Find-Item $srv $family 'Movie' 'Orbit of Glass'
        Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $hatedRec) -Token $family.Token -Body @{ value = -3 } | Out-Null

        $config.OverrideSuggestions = $true
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
        Check 'override on: hook Active' ((& $hookOf).state -eq 'Active') "$((& $hookOf).state) $((& $hookOf).message)"
        $ours = @((Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Recommendations?type=Movie&limit=5' -Token $family.Token).Items | ForEach-Object { $_.Id })
        $suggested = @((Invoke-Jf -Srv $srv -Method Get -Path $suggestPath -Token $family.Token).Items | ForEach-Object { $_.Id })
        Check 'override on: /Items/Suggestions = the recommendations' ($suggested.Count -gt 0 -and ($suggested -join ',') -eq ($ours -join ','))
        $episodes = @((Invoke-Jf -Srv $srv -Method Get -Path ('/Items/Suggestions?type=Episode&limit=3&userId=' + $family.Id) -Token $family.Token).Items | Where-Object { $_.Type -ne 'Episode' })
        Check 'override on: suggestions for other types are left alone' ($episodes.Count -eq 0)
        $categories = @(Invoke-Jf -Srv $srv -Method Get -Path ('/Movies/Recommendations?categoryLimit=5&itemLimit=12&userId=' + $family.Id) -Token $family.Token | ForEach-Object { $_ })
        $inCategories = @($categories | ForEach-Object { $_.Items } | ForEach-Object { $_.Id })
        Check 'override on: /Movies/Recommendations has no item rated -3' ($categories.Count -gt 0 -and -not ($inCategories -contains $hatedRec)) "categories=$($categories.Count)"

        $config.OverrideSuggestions = $savedOverride
        Invoke-Jf -Srv $srv -Method Post -Path $pluginPath -Token $admin.Token -Body $config | Out-Null
        Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $hatedRec) -Token $family.Token | Out-Null
        if (-not $wasPlayed) { Invoke-Jf -Srv $srv -Method Delete -Path ('/UserPlayedItems/' + $movie + '?userId=' + $family.Id) -Token $family.Token | Out-Null }
    }

    # Series rating modes and season ratings (design section 4.1).
    $seasons = Invoke-Jf -Srv $srv -Method Get -Path ('/Items?Recursive=true&IncludeItemTypes=Season&ParentId=' + $series + '&userId=' + $family.Id) -Token $family.Token
    $season1 = @($seasons.Items | Sort-Object IndexNumber)[0].Id
    $season2 = @($seasons.Items | Sort-Object IndexNumber)[1].Id
    foreach ($id in @($season1, $season2, $series)) { Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $family.Token | Out-Null }
    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = $null } | Out-Null
    $prefs = Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Preferences' -Token $family.Token
    Check 'default series mode is whole series' ($prefs.defaultSeriesMode -eq 'Series' -and $prefs.canChooseSeriesMode)
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $season1) -Token $family.Token -Body @{ value = 3 } }
    Check 'whole-series mode: a season cannot be rated (400)' ($status -eq 400) "got $status"
    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = 'Both' } | Out-Null
    Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $series) -Token $family.Token -Body @{ value = 1 } | Out-Null
    $s1 = Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $season1) -Token $family.Token -Body @{ value = 3 }
    Check 'both mode: series and seasons can be rated' ($s1.value -eq 3 -and $s1.itemType -eq 'Season')
    $prefs = Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = 'Season' }
    Check 'user can switch to per-season mode' ($prefs.seriesMode -eq 'Season' -and $prefs.userSeriesMode -eq 'Season')
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Put -Path ('/RatingsRecs/Ratings/' + $series) -Token $family.Token -Body @{ value = 3 } }
    Check 'per-season mode: the series itself cannot be rated (400)' ($status -eq 400) "got $status"
    $copy = Invoke-Jf -Srv $srv -Method Post -Path ('/RatingsRecs/Ratings/' + $series + '/CopyToSeasons') -Token $family.Token
    $s2 = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $season2) -Token $family.Token
    $s1 = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $season1) -Token $family.Token
    Check 'copy series rating fills only unrated seasons' ($copy.copied -eq 1 -and $s2.value -eq 1 -and $s1.value -eq 3) "copied=$($copy.copied) s1=$($s1.value) s2=$($s2.value)"
    Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = $null } | Out-Null
    $kept = @(Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings?itemIds=' + $series + ',' + $season1 + ',' + $season2) -Token $family.Token | ForEach-Object { $_ })
    Check 'switching mode never deletes ratings' ($kept.Count -eq 3) "kept $($kept.Count)"
    $status = Get-Status { Invoke-Jf -Srv $srv -Method Put -Path '/RatingsRecs/Preferences' -Token $family.Token -Body @{ seriesMode = 'Weekly' } }
    Check 'invalid mode -> 400' ($status -eq 400) "got $status"
    foreach ($id in @($season1, $season2)) { Invoke-Jf -Srv $srv -Method Delete -Path ('/RatingsRecs/Ratings/' + $id) -Token $family.Token | Out-Null }
    $statusInfo = Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Status' -Token $admin.Token
    Check 'status reports seasons without per-episode credits' ($null -ne $statusInfo.seasonCredits -and $statusInfo.seasonCredits.seasons -ge 4) "report: $($statusInfo.seasonCredits | ConvertTo-Json -Compress)"

    # Persistence after restart.
    if (-not $SkipRestart) {
        Invoke-Native docker @('compose', '-f', $srv.Compose, 'restart')
        Wait-JfReady $srv | Out-Null
        Start-Sleep -Seconds 5
        $admin = Login $srv $envValues['JF_ADMIN_USER'] $envValues['JF_ADMIN_PASSWORD']
        $afterRestart = Invoke-Jf -Srv $srv -Method Get -Path ('/RatingsRecs/Ratings/' + $series) -Token $admin.Token
        Check 'rating survives a server restart' ($afterRestart.value -eq 3)
    }
}

if ($script:Failures -gt 0) { Write-Host "$($script:Failures) check(s) failed" -ForegroundColor Red; exit 1 }
Write-Host 'All checks passed'
