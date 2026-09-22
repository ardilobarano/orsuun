# HTTP smoke test against a running dev server. Creates a throwaway account and walks every endpoint.
#   tools\smoke.ps1 [-BaseUrl http://localhost:5080]
param([string]$BaseUrl = "http://localhost:5080")

$ErrorActionPreference = 'Continue'
$j = @{ 'Content-Type' = 'application/json' }
function Rid { [guid]::NewGuid().ToString('N') }
function Fail($block) { try { & $block; "  (no error)" } catch { $r = $_.Exception.Response; $sr = New-Object IO.StreamReader($r.GetResponseStream()); "  $($r.StatusCode.value__) $($sr.ReadToEnd())" } }

$login = Invoke-RestMethod "$BaseUrl/v1/auth/guest" -Method Post -Headers $j -Body (@{ deviceToken = "smoke-" + (Rid) } | ConvertTo-Json)
$h = @{ 'Content-Type' = 'application/json'; 'X-Session' = $login.sessionToken }
"login: created=$($login.created)"

$me = Invoke-RestMethod "$BaseUrl/v1/me" -Headers $h
"me: hero atk=$($me.hero.attack) def=$($me.hero.defense) hp=$($me.hero.maxHp) items=$($me.items.Count) parked=$($me.parkedStage) highest=$($me.highestStageCleared)"
"park 2 while locked:"; Fail { Invoke-RestMethod "$BaseUrl/v1/park" -Method Post -Headers $h -Body (@{ stage = 2 } | ConvertTo-Json) }

for ($i = 0; $i -lt 3; $i++) {
    $pu = Invoke-RestMethod "$BaseUrl/v1/push" -Method Post -Headers $h -Body (@{ requestId = (Rid) } | ConvertTo-Json)
    "push: stage=$($pu.lastPush.stage) cleared=$($pu.lastPush.cleared) ticks=$($pu.lastPush.ticks) highest=$($pu.highestStageCleared) items=$($pu.items.Count)"
}
$pk = Invoke-RestMethod "$BaseUrl/v1/park" -Method Post -Headers $h -Body (@{ stage = 2 } | ConvertTo-Json); "park 2: parked=$($pk.parkedStage)"

$rid = Rid
$f = Invoke-RestMethod "$BaseUrl/v1/forge" -Method Post -Headers $h -Body (@{ requestId = $rid; method = 'ScrollOfMercy' } | ConvertTo-Json)
"forge: $($f.lastForge.outcome) +$($f.lastForge.levelBefore)->+$($f.lastForge.levelAfter) sorn=$($f.inventory.sorn)"
"forge replay:"; Fail { Invoke-RestMethod "$BaseUrl/v1/forge" -Method Post -Headers $h -Body (@{ requestId = $rid; method = 'ScrollOfMercy' } | ConvertTo-Json) }

$t = Invoke-RestMethod "$BaseUrl/v1/turn" -Method Post -Headers $h -Body (@{ requestId = (Rid) } | ConvertTo-Json); "turn: turnstones=$($t.inventory.turnstones)"

if ($env:PGPASSWORD) {
    $sql = Join-Path $env:TEMP "orsuun-backdate.sql"
    Set-Content $sql -Encoding ascii -Value ('update "Accounts" set "LastHeartbeatUtc" = (now() at time zone ''utc'') - interval ''6 hours'' where "Id" = ''' + $login.accountId + ''';')
    & "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U orsuun -h localhost -d orsuun -q -f $sql
    $hb = Invoke-RestMethod "$BaseUrl/v1/heartbeat" -Method Post -Headers $h
    "offline 6h on stage $($hb.parkedStage): packs=$($hb.settlement.packs) sorn+=$($hb.settlement.sornEarned) items=$($hb.items.Count) " + (($hb.items | Group-Object rarity | ForEach-Object { "$($_.Name):$($_.Count)" }) -join ' ')
    $loose = $hb.items | Where-Object { -not $_.equipped -and $_.slot -eq 'Armor' } | Select-Object -First 1
    if ($loose) {
        $eq = Invoke-RestMethod "$BaseUrl/v1/equip" -Method Post -Headers $h -Body (@{ requestId = (Rid); itemId = $loose.id } | ConvertTo-Json)
        "equip $($loose.name): def=$($eq.hero.defense) hp=$($eq.hero.maxHp)"
        "equip again:"; Fail { Invoke-RestMethod "$BaseUrl/v1/equip" -Method Post -Headers $h -Body (@{ requestId = (Rid); itemId = $loose.id } | ConvertTo-Json) }
    }
}
$g = Invoke-RestMethod "$BaseUrl/v1/dev/grant" -Method Post -Headers $h; "grant: sorn=$($g.inventory.sorn)"
