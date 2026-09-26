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
$g = Invoke-RestMethod "$BaseUrl/v1/dev/grant" -Method Post -Headers $h; "grant: sorn=$($g.inventory.sorn) level=$($g.inventory.level)"

# Zones: push to stage 5 so Gorak War Camp opens, park in a Hunting Ground and a Field, then fight a Commander.
while (($me = Invoke-RestMethod "$BaseUrl/v1/me" -Headers $h).highestStageCleared -lt 5) {
    $pu = Invoke-RestMethod "$BaseUrl/v1/push" -Method Post -Headers $h -Body (@{ requestId = (Rid) } | ConvertTo-Json)
    if (-not $pu.lastPush.cleared) { "push to $($pu.lastPush.stage) failed, granting"; Invoke-RestMethod "$BaseUrl/v1/dev/grant" -Method Post -Headers $h | Out-Null; $f2 = Invoke-RestMethod "$BaseUrl/v1/forge" -Method Post -Headers $h -Body (@{ requestId = (Rid); method = 'ScrollOfMercy' } | ConvertTo-Json) }
}
"campaign: highest=$($me.highestStageCleared)"
"park hunting ground:"; $pk = Invoke-RestMethod "$BaseUrl/v1/park" -Method Post -Headers $h -Body (@{ stage = 201 } | ConvertTo-Json); "  parked=$($pk.parkedStage)"
"park field IV while locked:"; Fail { Invoke-RestMethod "$BaseUrl/v1/park" -Method Post -Headers $h -Body (@{ stage = 214 } | ConvertTo-Json) }
$pk = Invoke-RestMethod "$BaseUrl/v1/park" -Method Post -Headers $h -Body (@{ stage = 211 } | ConvertTo-Json); "park field I: parked=$($pk.parkedStage)"
"bosses:"; $me.bosses | ForEach-Object { "  $($_.name)  up=$($_.up)  secondsLeft=$($_.secondsLeft)  mechanic=$($_.mechanic)" }
"boss fight while down:"; Fail { Invoke-RestMethod "$BaseUrl/v1/boss/fight" -Method Post -Headers $h -Body (@{ requestId = (Rid); bossId = 1 } | ConvertTo-Json) }
$up = Invoke-RestMethod "$BaseUrl/v1/dev/bosses-up" -Method Post -Headers $h
foreach ($id in 1, 2, 3) {
    $bf = Invoke-RestMethod "$BaseUrl/v1/boss/fight" -Method Post -Headers $h -Body (@{ requestId = (Rid); bossId = $id } | ConvertTo-Json)
    "fight $($bf.lastBossFight.bossId): damage=$($bf.lastBossFight.damage) killed=$($bf.lastBossFight.killed) rank=$($bf.lastBossFight.rank)`n  $($bf.lastBossFight.chest)"
}
"fight again same spawn:"; Fail { Invoke-RestMethod "$BaseUrl/v1/boss/fight" -Method Post -Headers $h -Body (@{ requestId = (Rid); bossId = 1 } | ConvertTo-Json) }
$me = Invoke-RestMethod "$BaseUrl/v1/me" -Headers $h; "after: shards=$($me.inventory.korshards -join ',') skins=$($me.inventory.skins -join ',') fought=" + (($me.bosses | ForEach-Object { $_.foughtThisSpawn }) -join ',')

# Sockets: the dev grant gave 3 shards of every rank; set one on the weapon, try a wrong-slot shard, clear a Dead Shard if one appears.
$weapon = $me.weapon
"weapon sockets: " + (($weapon.sockets | ForEach-Object { $_.text }) -join ' | ')
"armor shard on weapon:"; Fail { Invoke-RestMethod "$BaseUrl/v1/socket/insert" -Method Post -Headers $h -Body (@{ requestId = (Rid); itemId = $weapon.id; socketIndex = 0; type = 'Vigor'; rank = 0 } | ConvertTo-Json) }
$dead = $null
for ($i = 0; $i -lt $weapon.sockets.Count; $i++) {
    $si = Invoke-RestMethod "$BaseUrl/v1/socket/insert" -Method Post -Headers $h -Body (@{ requestId = (Rid); itemId = $weapon.id; socketIndex = $i; type = 'Piercer'; rank = 2 } | ConvertTo-Json)
    "socket $i`: success=$($si.lastSocket.success)  $($si.lastSocket.text)  atk=$($si.hero.attack)"
    if (-not $si.lastSocket.success) { $dead = $i }
}
if ($dead -ne $null) { $sc = Invoke-RestMethod "$BaseUrl/v1/socket/clear" -Method Post -Headers $h -Body (@{ requestId = (Rid); itemId = $weapon.id; socketIndex = $dead } | ConvertTo-Json); "clear: $($sc.lastSocket.text)" }
"taken socket:"; Fail { Invoke-RestMethod "$BaseUrl/v1/socket/insert" -Method Post -Headers $h -Body (@{ requestId = (Rid); itemId = $weapon.id; socketIndex = 0; type = 'Piercer'; rank = 2 } | ConvertTo-Json) }
