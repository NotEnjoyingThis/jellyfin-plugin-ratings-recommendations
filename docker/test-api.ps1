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
    [ValidateSet('10.11', '12', 'all')][string]$Server = 'all',
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
    $onboarding = Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Onboarding?limit=20' -Token $family.Token
    Check 'onboarding answers (items or hidden)' ($onboarding.Hidden -or @($onboarding.Items).Count -gt 0)
    $times = @()
    for ($i = 0; $i -lt 7; $i++) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Jf -Srv $srv -Method Get -Path '/RatingsRecs/Recommendations?limit=20' -Token $family.Token | Out-Null
        $sw.Stop()
        $times += $sw.ElapsedMilliseconds
    }
    $median = ($times | Sort-Object)[3]
    Check "recommendations answer from cache in < 200 ms (median $median ms)" ($median -lt 200)

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
